using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using PubQuizCreator.Data;

using A = DocumentFormat.OpenXml.Drawing;

using P = DocumentFormat.OpenXml.Presentation;

namespace PubQuizCreator.Services
{
    public class ExportService(MediaService mediaService, SettingsService settingsService)
    {
        #region Private Fields

        private readonly string answersPath = settingsService.GetTemplatePath("Answers");
        private readonly string questionsPath = settingsService.GetTemplatePath("Questions");

        #endregion Private Fields

        #region Public Methods

        public async Task<byte[]> ExportAsync(Round round, bool isAnswers, CancellationToken ct)
        {
            var templatePath = isAnswers
                ? answersPath
                : questionsPath;

            var templateBytes = await File.ReadAllBytesAsync(templatePath, ct);
            using var stream = new MemoryStream();
            stream.Write(templateBytes);
            stream.Position = 0;

            using (var doc = PresentationDocument.Open(stream, isEditable: true))
            {
                var presentationPart = doc.PresentationPart
                    ?? throw new InvalidOperationException("PresentationPart is null.");

                var slideParts = GetOrderedSlideParts(presentationPart);
                var templateMap = MapTemplateSlides(slideParts);

                var questionTemplate = templateMap.GetValueOrDefault(Constants.TemplateSlideQuestion);
                var contentTemplate = templateMap.GetValueOrDefault(Constants.TemplateSlideContent);
                var answerTemplate = templateMap.GetValueOrDefault(Constants.TemplateSlideAnswer);

                var slideIndex = 0;

                // Build new slides for each question slot
                var newSlides = new List<SlidePart>();

                foreach (var slot in round.Slots.OrderBy(s => s.Position))
                {
                    if (slot.Question == null) continue;

                    var hasMedia = slot.Question.MediaType == MediaType.Image
                        && !string.IsNullOrWhiteSpace(slot.Question.MediaFile);

                    var sourceTemplate = isAnswers
                        ? answerTemplate
                        : hasMedia
                        ? contentTemplate
                        : questionTemplate;

                    if (sourceTemplate == null) continue;

                    var clonedSlide = CloneSlidePart(presentationPart, sourceTemplate);

                    // Set the slide name for later identification
                    var cSld = clonedSlide.Slide?.CommonSlideData;

                    if (cSld != null)
                    {
                        slideIndex++;

                        cSld.Name = isAnswers
                            ? $"Answer{slideIndex}"
                            : $"Question{slideIndex}";
                    }

                    // Set title: "Frage {Position}"
                    SetShapeText(clonedSlide, Constants.TemplateShapeTitle,
                        $"Frage {slot.Position}");

                    // Set question text
                    SetShapeText(clonedSlide, Constants.TemplateShapeQuestion,
                        slot.Question.TextShort);

                    // Set answer text
                    SetShapeText(clonedSlide, Constants.TemplateShapeAnswer,
                        slot.Question.Answer);

                    // Add speaker notes
                    var notesText = !string.IsNullOrWhiteSpace(slot.Question.TextLong)
                        ? slot.Question.TextLong
                        : slot.Question.TextShort;

                    AddOrUpdateSpeakerNotes(presentationPart, clonedSlide, notesText);

                    // Replace media image if applicable
                    if (hasMedia)
                    {
                        var imageBytes = await mediaService.LoadAsync(
                            slot.Question.MediaFile!, ct);

                        ReplaceMediaImage(clonedSlide, imageBytes,
                            slot.Question.MediaFile!);
                    }

                    newSlides.Add(clonedSlide);
                }

                // Rebuild the slide order generically:
                // all slides before the template block + [question slides] + all slides after the template block
                RebuildSlideOrder(presentationPart, slideParts, templateMap, newSlides);
            }

            return ConvertPotxToPptx(stream.ToArray());
        }

        #endregion Public Methods

        #region Private Methods

        private static void AddOrUpdateSpeakerNotes(PresentationPart presentationPart,
            SlidePart slidePart, string notesText)
        {
            NotesSlidePart notesPart;

            if (slidePart.NotesSlidePart != null)
            {
                notesPart = slidePart.NotesSlidePart;
            }
            else
            {
                notesPart = slidePart.AddNewPart<NotesSlidePart>();

                // Link to the notes master
                var notesMasterPart = presentationPart.NotesMasterPart;
                if (notesMasterPart != null)
                {
                    notesPart.AddPart(notesMasterPart);
                }

                // Create the notes slide structure
                notesPart.NotesSlide = new NotesSlide(
                    new CommonSlideData(
                        new ShapeTree(
                            new P.NonVisualGroupShapeProperties(
                                new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                                new P.NonVisualGroupShapeDrawingProperties(),
                                new ApplicationNonVisualDrawingProperties()),
                            new GroupShapeProperties(
                                new A.TransformGroup(
                                    new A.Offset { X = 0, Y = 0 },
                                    new A.Extents { Cx = 0, Cy = 0 },
                                    new A.ChildOffset { X = 0, Y = 0 },
                                    new A.ChildExtents { Cx = 0, Cy = 0 })),

                            // Slide image placeholder
                            new P.Shape(
                                new P.NonVisualShapeProperties(
                                    new P.NonVisualDrawingProperties { Id = 2U, Name = "Slide Image" },
                                    new P.NonVisualShapeDrawingProperties(
                                        new A.ShapeLocks { NoGrouping = true, NoRotation = true, NoChangeAspect = true }),
                                    new ApplicationNonVisualDrawingProperties(
                                        new PlaceholderShape { Type = PlaceholderValues.SlideImage })),
                                new P.ShapeProperties()),

                            // Notes body placeholder
                            new P.Shape(
                                new P.NonVisualShapeProperties(
                                    new P.NonVisualDrawingProperties { Id = 3U, Name = "Notes Placeholder" },
                                    new P.NonVisualShapeDrawingProperties(
                                        new A.ShapeLocks { NoGrouping = true }),
                                    new ApplicationNonVisualDrawingProperties(
                                        new PlaceholderShape { Type = PlaceholderValues.Body, Index = 1U })),
                                new P.ShapeProperties(),
                                new P.TextBody(
                                    new A.BodyProperties(),
                                    new A.ListStyle(),
                                    new A.Paragraph(
                                        new A.Run(
                                            new A.RunProperties { Language = "en-US" },
                                            new A.Text(notesText))))))),
                    new ColorMapOverride(new A.MasterColorMapping()));

                return;
            }

            // Update existing notes: find the body placeholder and replace text
            var notesBody = notesPart.NotesSlide?
                .Descendants<P.Shape>()
                .FirstOrDefault(s =>
                {
                    var ph = s.NonVisualShapeProperties?
                        .ApplicationNonVisualDrawingProperties?
                        .GetFirstChild<PlaceholderShape>();
                    return ph?.Type?.Value == PlaceholderValues.Body;
                });

            if (notesBody?.TextBody != null)
            {
                var txBody = notesBody.TextBody;
                txBody.RemoveAllChildren<A.Paragraph>();
                txBody.Append(new A.Paragraph(
                    new A.Run(
                        new A.RunProperties { Language = "en-US" },
                        new A.Text(notesText))));
            }
        }

        private static SlidePart CloneSlidePart(PresentationPart presentationPart,
            SlidePart sourceSlide)
        {
            var newSlidePart = presentationPart.AddNewPart<SlidePart>();

            // Deep-copy the slide XML
            using (var sourceStream = sourceSlide.GetStream(FileMode.Open))
            {
                using var targetStream = newSlidePart.GetStream(FileMode.Create);
                sourceStream.CopyTo(targetStream);
            }

            // Re-create all relationships from the source slide
            foreach (var rel in sourceSlide.Parts)
            {
                if (rel.OpenXmlPart is ImagePart imagePart)
                {
                    // Clone image parts to avoid cross-references
                    var newImagePart = newSlidePart.AddImagePart(imagePart.ContentType, rel.RelationshipId);
                    using var imgStream = imagePart.GetStream(FileMode.Open);
                    newImagePart.FeedData(imgStream);
                }
                else
                {
                    newSlidePart.AddPart(rel.OpenXmlPart, rel.RelationshipId);
                }
            }

            // Copy external relationships (audio, video via URI)
            foreach (var extRel in sourceSlide.ExternalRelationships)
            {
                newSlidePart.AddExternalRelationship(
                    extRel.RelationshipType, extRel.Uri, extRel.Id);
            }

            // Copy hyperlink relationships
            foreach (var hypRel in sourceSlide.HyperlinkRelationships)
            {
                newSlidePart.AddHyperlinkRelationship(hypRel.Uri, hypRel.IsExternal, hypRel.Id);
            }

            // Copy audio/media data part references
            foreach (var dpRef in sourceSlide.DataPartReferenceRelationships)
            {
                switch (dpRef)
                {
                    case AudioReferenceRelationship audioRef:
                        newSlidePart.AddAudioReferenceRelationship(
                            (MediaDataPart)audioRef.DataPart, audioRef.Id);
                        break;

                    case MediaReferenceRelationship mediaRef:
                        newSlidePart.AddMediaReferenceRelationship(
                            (MediaDataPart)mediaRef.DataPart, mediaRef.Id);
                        break;

                    case VideoReferenceRelationship videoRef:
                        newSlidePart.AddVideoReferenceRelationship(
                            (MediaDataPart)videoRef.DataPart, videoRef.Id);
                        break;
                }
            }

            // Detach the cloned notes slide reference — notes are added separately
            var existingNotesPart = newSlidePart.NotesSlidePart;
            if (existingNotesPart != null)
            {
                newSlidePart.DeletePart(existingNotesPart);
            }

            return newSlidePart;
        }

        private static byte[] ConvertPotxToPptx(byte[] potxBytes)
        {
            using var input = new MemoryStream(potxBytes);
            using var output = new MemoryStream();

            using (var zipIn = new ZipArchive(input, ZipArchiveMode.Read))
            using (var zipOut = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var entry in zipIn.Entries)
                {
                    var newEntry = zipOut.CreateEntry(entry.FullName, CompressionLevel.Optimal);

                    using var reader = entry.Open();
                    using var writer = newEntry.Open();

                    if (entry.FullName == "[Content_Types].xml")
                    {
                        using var sr = new StreamReader(reader);
                        var content = sr.ReadToEnd()
                            .Replace(
                                "presentationml.template.main+xml",
                                "presentationml.presentation.main+xml");

                        using var sw = new StreamWriter(writer);
                        sw.Write(content);
                    }
                    else
                    {
                        reader.CopyTo(writer);
                    }
                }
            }

            return output.ToArray();
        }

        private static P.Shape? FindShapeByName(Slide slide, string name)
        {
            return slide.Descendants<P.Shape>()
                .FirstOrDefault(sp =>
                {
                    var cNvPr = sp.NonVisualShapeProperties?
                        .NonVisualDrawingProperties;
                    return cNvPr?.Name?.Value == name;
                });
        }

        private static string GetImageContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "image/png"
            };
        }

        private static List<SlidePart> GetOrderedSlideParts(PresentationPart presentationPart)
        {
            var presentation = presentationPart.Presentation;
            var slideIdList = presentation.SlideIdList
                ?? throw new InvalidOperationException("SlideIdList is null.");

            return slideIdList.Elements<SlideId>()
                .Select(sid => (SlidePart)presentationPart.GetPartById(sid.RelationshipId!))
                .ToList();
        }

        private static Dictionary<string, SlidePart> MapTemplateSlides(List<SlidePart> slideParts)
        {
            var result = new Dictionary<string, SlidePart>();

            foreach (var sp in slideParts)
            {
                var cSld = sp.Slide.CommonSlideData;
                var name = cSld?.Name?.Value;

                if (!string.IsNullOrEmpty(name))
                {
                    result[name] = sp;
                }
            }

            return result;
        }

        private static void RebuildSlideOrder(PresentationPart presentationPart, List<SlidePart> originalOrder,
            Dictionary<string, SlidePart> templateMap, List<SlidePart> questionSlides)
        {
            var presentation = presentationPart.Presentation;
            var slideIdList = presentation.SlideIdList!;

            // Collect all template slides that should be removed
            var templateSlideNames = new[]
            {
                Constants.TemplateSlideQuestion,
                Constants.TemplateSlideContent,
                Constants.TemplateSlideAnswer
            };

            var slidesToRemove = templateSlideNames
                .Select(name => templateMap.GetValueOrDefault(name))
                .Where(sp => sp != null)
                .ToHashSet()!;

            // Find the index range of template slides in the original order
            var templateIndices = originalOrder
                .Select((sp, i) => (sp, i))
                .Where(x => slidesToRemove.Contains(x.sp))
                .Select(x => x.i)
                .ToList();

            // If no template slides found, append question slides at the end
            int insertAt = templateIndices.Count > 0
                ? templateIndices.Min()
                : originalOrder.Count;

            int removeThrough = templateIndices.Count > 0
                ? templateIndices.Max()
                : insertAt - 1;

            // Build desired order: prefix + question slides + suffix
            var desiredOrder = originalOrder
                .Take(insertAt)
                .Concat(questionSlides)
                .Concat(originalOrder.Skip(removeThrough + 1))
                .ToList();

            // Remove template slides from the package
            foreach (var slideToRemove in slidesToRemove)
            {
                var notesPart = slideToRemove.NotesSlidePart;
                if (notesPart != null)
                    slideToRemove.DeletePart(notesPart);

                presentationPart.DeletePart(slideToRemove);
            }

            // Rebuild the SlideIdList
            slideIdList.RemoveAllChildren<SlideId>();

            uint nextId = 256;
            foreach (var slidePart in desiredOrder)
            {
                var relId = presentationPart.GetIdOfPart(slidePart);
                slideIdList.Append(new SlideId
                {
                    Id = nextId++,
                    RelationshipId = relId
                });
            }

            presentation.Save();
        }

        private static void ReplaceMediaImage(SlidePart slidePart, byte[] imageBytes, string fileName)
        {
            var slide = slidePart.Slide;

            // Find the p:pic element with cNvPr name="Media"
            var mediaPic = slide.Descendants<P.Picture>()
                .FirstOrDefault(pic =>
                {
                    var cNvPr = pic.NonVisualPictureProperties?
                        .NonVisualDrawingProperties;
                    return cNvPr?.Name?.Value == Constants.TemplateShapeMedia;
                });

            if (mediaPic == null) return;

            // Get the blip reference
            var blip = mediaPic.BlipFill?.Blip;
            if (blip?.Embed?.Value == null) return;

            var relId = blip.Embed.Value;

            // Get the existing image part and replace its content
            if (slidePart.TryGetPartById(relId, out var part) && part is ImagePart existingImage)
            {
                using var ms = new MemoryStream(imageBytes);
                existingImage.FeedData(ms);
            }
            else
            {
                // Fallback: create a new image part and update the reference
                var contentType = GetImageContentType(fileName);
                var newImagePart = slidePart.AddImagePart(contentType);
                using var ms = new MemoryStream(imageBytes);
                newImagePart.FeedData(ms);
                blip.Embed = slidePart.GetIdOfPart(newImagePart);
            }
        }

        private static void SetShapeText(SlidePart slidePart, string shapeName, string text)
        {
            var slide = slidePart.Slide;
            var shape = FindShapeByName(slide, shapeName);
            if (shape == null) return;

            var txBody = shape.TextBody;
            if (txBody == null) return;

            // Preserve the first paragraph's properties
            var firstPara = txBody.Elements<A.Paragraph>().FirstOrDefault();
            var paraProps = firstPara?.ParagraphProperties?.CloneNode(true) as A.ParagraphProperties;

            // Preserve the first run's properties
            var firstRun = firstPara?.Elements<A.Run>().FirstOrDefault();
            var runProps = firstRun?.RunProperties?.CloneNode(true) as A.RunProperties;

            // Remove all existing paragraphs
            txBody.RemoveAllChildren<A.Paragraph>();

            // Create new paragraph with preserved formatting
            var newPara = new A.Paragraph();
            if (paraProps != null) newPara.Append(paraProps);

            var newRun = new A.Run();
            if (runProps != null)
            {
                // Clear spell-check error marking from cloned properties
                runProps.Dirty = null;
                runProps.SpellingError = null;
                newRun.Append(runProps);
            }

            newRun.Append(new A.Text(text));
            newPara.Append(newRun);
            txBody.Append(newPara);
        }

        #endregion Private Methods
    }
}