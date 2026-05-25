using System.IO.Compression;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using Drawing = DocumentFormat.OpenXml.Drawing;

namespace PubQuizCreator.Services
{
    public class ExportService(MediaService mediaService, SettingsService settingsService)
    {
        #region Public Methods

        public async Task<byte[]> ExportAsync(Round round, string templatePath, CancellationToken ct)
        {
            var templateBytes = await File.ReadAllBytesAsync(templatePath, ct);

            using var stream = new MemoryStream();
            stream.Write(templateBytes);
            stream.Position = 0;

            using (var doc = PresentationDocument.Open(stream: stream, isEditable: true))
            {
                var presentationPart = doc.PresentationPart
                    ?? throw new InvalidOperationException("PresentationPart is null.");

                var slideParts = GetOrderedSlideParts(presentationPart);
                var templateMap = MapTemplateSlides(slideParts);

                var questionTemplate = templateMap.GetValueOrDefault(Constants.TemplateSlideQuestion);
                var contentTemplate = templateMap.GetValueOrDefault(Constants.TemplateSlideContent);
                var answerTemplate = templateMap.GetValueOrDefault(Constants.TemplateSlideAnswer);

                var titleFormat = settingsService.GetFormatTitle();
                var slideIndex = 0;
                var newSlides = new List<SlidePart>();

                foreach (var slot in round.Slots.OrderBy(s => s.Position))
                {
                    if (slot.Question == null) continue;

                    var hasMedia = slot.Question.MediaType == MediaType.Image
                        && !string.IsNullOrWhiteSpace(slot.Question.MediaFile);

                    // Use answer template if available, otherwise content or question template
                    var sourceTemplate = answerTemplate
                        ?? (hasMedia ? contentTemplate : questionTemplate)
                        ?? contentTemplate
                        ?? questionTemplate;

                    if (sourceTemplate == null) continue;

                    var clonedSlide = CloneSlidePart(
                        presentationPart: presentationPart,
                        sourceSlide: sourceTemplate);

                    var cSld = clonedSlide.Slide?.CommonSlideData;

                    if (cSld != null)
                    {
                        slideIndex++;
                        cSld.Name = $"Slide{slideIndex}";
                    }

                    var title = titleFormat.Replace("{position}", slot.Position.ToString());

                    // Populate all shapes — missing shapes are silently skipped
                    SetShapeText(clonedSlide, Constants.TemplateShapeTitle, title);
                    SetShapeText(clonedSlide, Constants.TemplateShapeQuestion, slot.Question.TextShort);
                    SetShapeText(clonedSlide, Constants.TemplateShapeAnswer, slot.Question.Answer);

                    var notesText = !string.IsNullOrWhiteSpace(slot.Question.TextLong)
                        ? slot.Question.TextLong
                        : slot.Question.TextShort;

                    SetSpeakerNotes(presentationPart, clonedSlide, notesText);

                    if (hasMedia)
                    {
                        byte[]? imageBytes = null;

                        try
                        {
                            imageBytes = await mediaService.LoadAsync(
                                fileName: slot.Question.MediaFile!,
                                ct: ct);
                        }
                        catch (FileNotFoundException) { }

                        if (imageBytes != null)
                        {
                            ReplaceMediaImage(
                                slidePart: clonedSlide,
                                imageBytes: imageBytes,
                                fileName: slot.Question.MediaFile!);
                        }
                    }

                    newSlides.Add(clonedSlide);
                }

                RebuildSlideOrder(
                    presentationPart: presentationPart,
                    originalOrder: slideParts,
                    templateMap: templateMap,
                    questionSlides: newSlides);
            }

            return ConvertPotxToPptx(stream.ToArray());
        }

        #endregion Public Methods

        #region Private Methods

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
                    var newImagePart = newSlidePart.AddImagePart(
                        contentType: imagePart.ContentType,
                        id: rel.RelationshipId);

                    using var imgStream = imagePart.GetStream(FileMode.Open);
                    newImagePart.FeedData(imgStream);
                }
                else
                {
                    newSlidePart.AddPart(
                        part: rel.OpenXmlPart,
                        id: rel.RelationshipId);
                }
            }

            // Copy external relationships (audio, video via URI)
            foreach (var extRel in sourceSlide.ExternalRelationships)
            {
                newSlidePart.AddExternalRelationship(
                    relationshipType: extRel.RelationshipType,
                    externalUri: extRel.Uri,
                    id: extRel.Id);
            }

            // Copy hyperlink relationships
            foreach (var hypRel in sourceSlide.HyperlinkRelationships)
            {
                newSlidePart.AddHyperlinkRelationship(
                    hyperlinkUri: hypRel.Uri,
                    isExternal: hypRel.IsExternal,
                    id: hypRel.Id);
            }

            // Copy audio/media data part references
            foreach (var dpRef in sourceSlide.DataPartReferenceRelationships)
            {
                switch (dpRef)
                {
                    case AudioReferenceRelationship audioRef:
                        newSlidePart.AddAudioReferenceRelationship(
                            mediaDataPart: (MediaDataPart)audioRef.DataPart,
                            id: audioRef.Id);
                        break;

                    case MediaReferenceRelationship mediaRef:
                        newSlidePart.AddMediaReferenceRelationship(
                            mediaDataPart: (MediaDataPart)mediaRef.DataPart,
                            id: mediaRef.Id);
                        break;

                    case VideoReferenceRelationship videoRef:
                        newSlidePart.AddVideoReferenceRelationship(
                            mediaDataPart: (MediaDataPart)videoRef.DataPart,
                            id: videoRef.Id);
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

            using (var zipIn = new ZipArchive(
                stream: input,
                mode: ZipArchiveMode.Read))
            {
                using var zipOut = new ZipArchive(
                    stream: output,
                    mode: ZipArchiveMode.Create,
                    leaveOpen: true);

                foreach (var entry in zipIn.Entries)
                {
                    var newEntry = zipOut.CreateEntry(
                        entryName: entry.FullName,
                        compressionLevel: CompressionLevel.Optimal);

                    using var reader = entry.Open();
                    using var writer = newEntry.Open();

                    if (entry.FullName == "[Content_Types].xml")
                    {
                        using var sr = new StreamReader(reader);

                        var content = sr.ReadToEnd()
                            .Replace(
                                oldValue: "presentationml.template.main+xml",
                                newValue: "presentationml.presentation.main+xml");

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

        private static Shape? FindShapeByName(Slide slide, string name)
        {
            return slide.Descendants<Shape>()
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
            var slideIdList = presentation?.SlideIdList
                ?? throw new InvalidOperationException("SlideIdList is null.");

            return slideIdList.Elements<SlideId>()
                .Select(sid => (SlidePart)presentationPart.GetPartById(sid.RelationshipId!)).ToList();
        }

        private static Dictionary<string, SlidePart> MapTemplateSlides(List<SlidePart> slideParts)
        {
            var result = new Dictionary<string, SlidePart>();

            foreach (var sp in slideParts)
            {
                var cSld = sp.Slide?.CommonSlideData;
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
            var slideIdList = presentation?.SlideIdList!;

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
                if (slideToRemove != default)
                {
                    var notesPart = slideToRemove?.NotesSlidePart;

                    if (notesPart != null)
                        slideToRemove!.DeletePart(notesPart);

                    presentationPart.DeletePart(slideToRemove!);
                }
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

            presentation!.Save();
        }

        private static void ReplaceMediaImage(SlidePart slidePart, byte[] imageBytes, string fileName)
        {
            var slide = slidePart.Slide;

            // Find the p:pic element with cNvPr name="Media"
            var mediaPic = slide?.Descendants<Picture>()
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

            if (slide == default) return;

            var shape = FindShapeByName(
                slide: slide,
                name: shapeName);
            if (shape == null) return;

            var txBody = shape.TextBody;
            if (txBody == null) return;

            // Preserve the first paragraph's properties
            var firstPara = txBody.Elements<Drawing.Paragraph>().FirstOrDefault();

            // Preserve the first run's properties
            var firstRun = firstPara?.Elements<Drawing.Run>().FirstOrDefault();

            // Remove all existing paragraphs
            txBody.RemoveAllChildren<Drawing.Paragraph>();

            // Create new paragraph with preserved formatting
            var newPara = new Drawing.Paragraph();
            if (firstPara?.ParagraphProperties?.CloneNode(true) is Drawing.ParagraphProperties paraProps)
                newPara.Append(paraProps);

            var newRun = new Drawing.Run();
            if (firstRun?.RunProperties?.CloneNode(true) is Drawing.RunProperties runProps)
            {
                // Clear spell-check error marking from cloned properties
                runProps.Dirty = null;
                runProps.SpellingError = null;
                newRun.Append(runProps);
            }

            newRun.Append(new Drawing.Text(text));
            newPara.Append(newRun);
            txBody.Append(newPara);
        }

        private static void SetSpeakerNotes(PresentationPart presentationPart, SlidePart slidePart, string notesText)
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
                            new NonVisualGroupShapeProperties(
                                new NonVisualDrawingProperties { Id = 1U, Name = "" },
                                new NonVisualGroupShapeDrawingProperties(),
                                new ApplicationNonVisualDrawingProperties()),
                            new GroupShapeProperties(
                                new Drawing.TransformGroup(
                                    new Drawing.Offset { X = 0, Y = 0 },
                                    new Drawing.Extents { Cx = 0, Cy = 0 },
                                    new Drawing.ChildOffset { X = 0, Y = 0 },
                                    new Drawing.ChildExtents { Cx = 0, Cy = 0 })),

                            // Slide image placeholder
                            new Shape(
                                new NonVisualShapeProperties(
                                    new NonVisualDrawingProperties { Id = 2U, Name = "Slide Image" },
                                    new NonVisualShapeDrawingProperties(
                                        new Drawing.ShapeLocks { NoGrouping = true, NoRotation = true, NoChangeAspect = true }),
                                    new ApplicationNonVisualDrawingProperties(
                                        new PlaceholderShape { Type = PlaceholderValues.SlideImage })),
                                new ShapeProperties()),

                            // Notes body placeholder
                            new Shape(
                                new NonVisualShapeProperties(
                                    new NonVisualDrawingProperties { Id = 3U, Name = "Notes Placeholder" },
                                    new NonVisualShapeDrawingProperties(
                                        new Drawing.ShapeLocks { NoGrouping = true }),
                                    new ApplicationNonVisualDrawingProperties(
                                        new PlaceholderShape { Type = PlaceholderValues.Body, Index = 1U })),
                                new ShapeProperties(),
                                new TextBody(
                                    new Drawing.BodyProperties(),
                                    new Drawing.ListStyle(),
                                    new Drawing.Paragraph(
                                        new Drawing.Run(
                                            new Drawing.RunProperties { Language = "en-US" },
                                            new Drawing.Text(notesText))))))),
                    new ColorMapOverride(new Drawing.MasterColorMapping()));

                return;
            }

            // Update existing notes: find the body placeholder and replace text
            var notesBody = notesPart.NotesSlide?
                .Descendants<Shape>()
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
                txBody.RemoveAllChildren<Drawing.Paragraph>();

                txBody.Append(new Drawing.Paragraph(
                    new Drawing.Run(
                        new Drawing.RunProperties { Language = "en-US" },
                        new Drawing.Text(notesText))));
            }
        }

        #endregion Private Methods
    }
}