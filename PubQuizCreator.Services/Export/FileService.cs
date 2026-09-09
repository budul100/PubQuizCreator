using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using PubQuizCreator.Services.App;
using PubQuizCreator.Services.Content;
using Drawing = DocumentFormat.OpenXml.Drawing;

namespace PubQuizCreator.Services.Export
{
    public class FileService(MediaService mediaService, SettingsService settingsService, ToastService toastService)
    {
        #region Public Methods

        public async Task<byte[]> ExportAsync(Round round, string templatePath, CancellationToken ct)
        {
            var templateBytes = await File.ReadAllBytesAsync(
                path: templatePath,
                cancellationToken: ct);

            using var stream = new MemoryStream();

            await stream.WriteAsync(
                buffer: templateBytes.AsMemory(0, templateBytes.Length),
                cancellationToken: ct);

            stream.Position = 0;

            // 1. Wrap PresentationDocument in an explicit using block to flush all XML parts
            // and relationships into the memory stream upon closing.
            using (var doc = PresentationDocument.Open(
                stream: stream, 
                isEditable: true))
            {
                var presentationPart = doc.PresentationPart
                    ?? throw new InvalidOperationException("PresentationPart is null.");

                var slideParts = GetOrderedSlideParts(presentationPart);
                var templateMap = MapTemplateSlides(slideParts);

                var questionTemplate = templateMap.GetValueOrDefault(Constants.TemplateSlideQuestion);
                var mediaTemplate = templateMap.GetValueOrDefault(Constants.TemplateSlideMedia);
                var answerTemplate = templateMap.GetValueOrDefault(Constants.TemplateSlideAnswer);

                var titleFormat = settingsService.GetFormatTitle();
                var slideIndex = 0;
                var newSlides = new List<SlidePart>();

                var slots = round.Slots
                    .OrderBy(s => s.Position).ToArray();

                foreach (var slot in slots)
                {
                    if (slot.Question == null) continue;

                    var hasMedia = !string.IsNullOrWhiteSpace(slot.Question.MediaFile)
                        && slot.Question.MediaType is MediaType.Image or MediaType.Video;

                    var sourceTemplate = (hasMedia ? mediaTemplate : default)
                        ?? questionTemplate
                        ?? answerTemplate;

                    if (sourceTemplate == null) continue;

                    var clonedSlide = GetSlidePart(
                        presentationPart: presentationPart,
                        sourceSlide: sourceTemplate);

                    slideIndex++;

                    UpdateSlideIdentifier(
                        slidePart: clonedSlide,
                        slideName: $"Slide{slideIndex}");

                    var title = titleFormat.Replace(
                        oldValue: "{position}",
                        newValue: slot.Position.ToString());

                    var description = GetDescription(
                        questionText: slot.Question.Text,
                        description: slot.Question.Description);

                    SetShapeText(
                        slidePart: clonedSlide,
                        shapeName: Constants.TemplateShapeTitle,
                        text: title);

                    SetShapeText(
                        slidePart: clonedSlide,
                        shapeName: Constants.TemplateShapeQuestion,
                        text: slot.Question.Text);

                    SetShapeText(
                        slidePart: clonedSlide,
                        shapeName: Constants.TemplateShapeQuestionDescription,
                        text: description);

                    SetShapeText(
                        slidePart: clonedSlide,
                        shapeName: Constants.TemplateShapeAnswer,
                        text: slot.Question.Answer);

                    SetSpeakerNotes(
                        presentationPart: presentationPart,
                        slidePart: clonedSlide,
                        text: description);

                    if (hasMedia)
                    {
                        await TryAttachMediaAsync(
                            slidePart: clonedSlide,
                            mediaFileName: slot.Question.MediaFile!,
                            mediaType: slot.Question.MediaType,
                            ct: ct);
                    }

                    // Flush DOM changes of the cloned slide into its part stream
                    clonedSlide.Slide?.Save();

                    newSlides.Add(clonedSlide);
                }

                RebuildSlideOrder(
                    presentationPart: presentationPart,
                    originalOrder: slideParts,
                    templateMap: templateMap,
                    questionSlides: newSlides);

                presentationPart.Presentation?.Save();
            }

            // 2. Convert POTX to PPTX only after 'doc' has been closed and fully flushed
            return ConvertPotxToPptx(stream.ToArray());
        }

        #endregion Public Methods

        #region Private Methods

        private static byte[] ConvertPotxToPptx(byte[] potxBytes)
        {
            using var input = new MemoryStream(potxBytes);
            using var output = new MemoryStream();

            using (var zipIn = new ZipArchive(
                stream: input, 
                mode: ZipArchiveMode.Read))
            {
                // zipOut MUST be disposed before output.ToArray(),
                // as ZipArchive writes the central directory record upon disposal.
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
                        using var sr = new StreamReader(reader, Encoding.UTF8);
                        var content = sr.ReadToEnd().Replace(
                            oldValue: "presentationml.template.main+xml",
                            newValue: "presentationml.presentation.main+xml");

                        using var sw = new StreamWriter(writer, new UTF8Encoding(false));
                        sw.Write(content);
                    }
                    else
                    {
                        reader.CopyTo(writer);
                    }
                }

                // zipOut is closed here -> Central directory record is written to 'output'
            }

            return output.ToArray();
        }

        private static NotesSlidePart CreateNotesSlidePart(PresentationPart presentationPart, SlidePart slidePart)
        {
            var notesPart = slidePart.AddNewPart<NotesSlidePart>();

            // 1. Link to the NotesMasterPart
            if (presentationPart.NotesMasterPart is { } notesMasterPart)
            {
                notesPart.AddPart(notesMasterPart);
            }

            // 2. IMPORTANT: Bidirectional link back to the SlidePart!
            // Without this relationship, PowerPoint considers the slide notes corrupted.
            notesPart.AddPart(slidePart);

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
                        new Shape(
                            new NonVisualShapeProperties(
                                new NonVisualDrawingProperties { Id = 2U, Name = "Slide Image" },
                                new NonVisualShapeDrawingProperties(
                                    new Drawing.ShapeLocks { NoGrouping = true, NoRotation = true, NoChangeAspect = true }),
                                new ApplicationNonVisualDrawingProperties(
                                    new PlaceholderShape { Type = PlaceholderValues.SlideImage })),
                            new ShapeProperties()),
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
                                new Drawing.ListStyle())))),
                new ColorMapOverride(new Drawing.MasterColorMapping()));

            notesPart.NotesSlide.Save();
            return notesPart;
        }

        private static Shape? FindShapeByName(Slide slide, string name)
        {
            return slide.Descendants<Shape>()
                .FirstOrDefault(sp => sp.NonVisualShapeProperties?
                    .NonVisualDrawingProperties?.Name?.Value == name);
        }

        private static string GetDescription(string? questionText, string? description)
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(questionText))
            {
                builder.AppendLine(questionText);
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                builder.AppendLine(description);
            }

            return builder.ToString();
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
            var slideIdList = presentationPart.Presentation?.SlideIdList
                ?? throw new InvalidOperationException("SlideIdList is null.");

            return slideIdList.Elements<SlideId>()
                .Select(sid => (SlidePart)presentationPart.GetPartById(sid.RelationshipId!)).ToList();
        }

        private static SlidePart GetSlidePart(PresentationPart presentationPart, SlidePart sourceSlide)
        {
            var newSlidePart = presentationPart.AddNewPart<SlidePart>();

            // 1. Explicitly attach the SlideLayoutPart using the exact original relationship ID
            if (sourceSlide.SlideLayoutPart is { } layoutPart)
            {
                var layoutRelId = sourceSlide.GetIdOfPart(layoutPart);
                newSlidePart.AddPart(layoutPart, layoutRelId);
            }

            // 2. Clone the slide XML content
            using (var sourceStream = sourceSlide.GetStream(FileMode.Open))
            using (var targetStream = newSlidePart.GetStream(FileMode.Create))
            {
                sourceStream.CopyTo(targetStream);
            }

            // 3. Copy remaining parts (images, embedded parts, etc.)
            foreach (var rel in sourceSlide.Parts)
            {
                if (rel.OpenXmlPart is SlideLayoutPart or NotesSlidePart)
                    continue;

                if (rel.OpenXmlPart is ImagePart imagePart)
                {
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

            // 4. Copy external relationships
            foreach (var extRel in sourceSlide.ExternalRelationships)
            {
                newSlidePart.AddExternalRelationship(
                    relationshipType: extRel.RelationshipType,
                    externalUri: extRel.Uri,
                    id: extRel.Id);
            }

            // 5. Copy hyperlink relationships
            foreach (var hypRel in sourceSlide.HyperlinkRelationships)
            {
                newSlidePart.AddHyperlinkRelationship(
                    hyperlinkUri: hypRel.Uri,
                    isExternal: hypRel.IsExternal,
                    id: hypRel.Id);
            }

            // 6. Copy media data part references (audio/video)
            var packageDoc = (PresentationDocument)newSlidePart.OpenXmlPackage;

            foreach (var dpRef in sourceSlide.DataPartReferenceRelationships)
            {
                if (dpRef.DataPart is MediaDataPart originalMedia)
                {
                    var clonedMediaPart = packageDoc.CreateMediaDataPart(originalMedia.ContentType);

                    using (var src = originalMedia.GetStream(FileMode.Open))
                    {
                        clonedMediaPart.FeedData(src);
                    }

                    switch (dpRef)
                    {
                        case AudioReferenceRelationship audioRef:
                            newSlidePart.AddAudioReferenceRelationship(
                                mediaDataPart: clonedMediaPart,
                                id: audioRef.Id);
                            break;

                        case MediaReferenceRelationship mediaRef:
                            newSlidePart.AddMediaReferenceRelationship(
                                mediaDataPart: clonedMediaPart,
                                id: mediaRef.Id);
                            break;

                        case VideoReferenceRelationship videoRef:
                            newSlidePart.AddVideoReferenceRelationship(
                                mediaDataPart: clonedMediaPart,
                                id: videoRef.Id);
                            break;
                    }
                }
            }

            // Remove any legacy notes slide that might have been copied via relationships
            if (newSlidePart.NotesSlidePart is { } existingNotesPart)
            {
                newSlidePart.DeletePart(existingNotesPart);
            }

            return newSlidePart;
        }

        private static Dictionary<string, SlidePart> MapTemplateSlides(List<SlidePart> slideParts)
        {
            var result = new Dictionary<string, SlidePart>(StringComparer.OrdinalIgnoreCase);

            foreach (var sp in slideParts)
            {
                var name = sp.Slide?.CommonSlideData?.Name?.Value;
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
            var slideIdList = presentation?.SlideIdList
                ?? throw new InvalidOperationException("SlideIdList is null.");

            var templateSlideNames = new[]
            {
                Constants.TemplateSlideQuestion,
                Constants.TemplateSlideMedia,
                Constants.TemplateSlideAnswer
            };

            var slidesToRemove = templateSlideNames
                .Select(name => templateMap.GetValueOrDefault(name))
                .OfType<SlidePart>().ToHashSet();

            var templateIndices = originalOrder
                .Select((sp, i) => (SlidePart: sp, Index: i))
                .Where(x => slidesToRemove.Contains(x.SlidePart))
                .Select(x => x.Index).ToList();

            var insertAt = templateIndices.Count > 0
                ? templateIndices.Min()
                : originalOrder.Count;

            var removeThrough = templateIndices.Count > 0
                ? templateIndices.Max()
                : insertAt - 1;

            var desiredOrder = originalOrder
                .Take(insertAt)
                .Concat(questionSlides)
                .Concat(originalOrder.Skip(removeThrough + 1)).ToList();

            foreach (var slideToRemove in slidesToRemove)
            {
                if (slideToRemove.NotesSlidePart is { } notesPart)
                {
                    slideToRemove.DeletePart(notesPart);
                }

                presentationPart.DeletePart(slideToRemove);
            }

            slideIdList.RemoveAllChildren<SlideId>();

            uint nextId = 256;
            foreach (var slidePart in desiredOrder)
            {
                slideIdList.Append(new SlideId
                {
                    Id = nextId++,
                    RelationshipId = presentationPart.GetIdOfPart(slidePart)
                });
            }

            presentation.Save();
        }

        private static void ReplaceMediaAudio(SlidePart slidePart, byte[] audioBytes)
        {
            var audioRel = slidePart.DataPartReferenceRelationships
                .OfType<AudioReferenceRelationship>()
                .FirstOrDefault();

            if (audioRel?.DataPart is MediaDataPart targetMediaPart)
            {
                using var ms = new MemoryStream(audioBytes);
                targetMediaPart.FeedData(ms);
            }
        }

        private static void ReplaceMediaImage(SlidePart slidePart, byte[] imageBytes, string fileName)
        {
            var slide = slidePart.Slide;

            var mediaPic = slide?.Descendants<Picture>()
                .FirstOrDefault(pic => pic.NonVisualPictureProperties?
                    .NonVisualDrawingProperties?.Name?.Value == Constants.TemplateShapeMedia);

            if (mediaPic?.BlipFill?.Blip is not { Embed.Value: { } relId } blip)
                return;

            if (slidePart.TryGetPartById(
                id: relId,
                part: out var part)
                && part is ImagePart existingImage)
            {
                using var ms = new MemoryStream(imageBytes);
                existingImage.FeedData(ms);
            }
            else
            {
                var contentType = GetImageContentType(fileName);
                var newImagePart = slidePart.AddImagePart(contentType);

                using var ms = new MemoryStream(imageBytes);
                newImagePart.FeedData(ms);

                blip.Embed = slidePart.GetIdOfPart(newImagePart);
            }
        }

        private static void SetShapeText(SlidePart slidePart, string shapeName, string? text)
        {
            if (slidePart.Slide == null || string.IsNullOrEmpty(text))
                return;

            var shape = FindShapeByName(slide: slidePart.Slide, name: shapeName);
            var txBody = shape?.TextBody;
            if (txBody == null) return;

            var firstPara = txBody.Elements<Drawing.Paragraph>().FirstOrDefault();
            var firstRun = firstPara?.Elements<Drawing.Run>().FirstOrDefault();

            txBody.RemoveAllChildren<Drawing.Paragraph>();

            var newPara = new Drawing.Paragraph();
            if (firstPara?.ParagraphProperties?.CloneNode(true) is Drawing.ParagraphProperties paraProps)
            {
                newPara.Append(paraProps);
            }

            var newRun = new Drawing.Run();
            if (firstRun?.RunProperties?.CloneNode(true) is Drawing.RunProperties runProps)
            {
                runProps.Dirty = null;
                runProps.SpellingError = null;
                newRun.Append(runProps);
            }

            newRun.Append(new Drawing.Text(text));
            newPara.Append(newRun);
            txBody.Append(newPara);
        }

        private static void SetSpeakerNotes(PresentationPart presentationPart, SlidePart slidePart, string text)
        {
            var notesPart = slidePart.NotesSlidePart ?? CreateNotesSlidePart(presentationPart, slidePart);

            var notesBody = notesPart.NotesSlide?
                .Descendants<Shape>()
                .FirstOrDefault(s => s.NonVisualShapeProperties?
                    .ApplicationNonVisualDrawingProperties?
                    .GetFirstChild<PlaceholderShape>()?.Type?.Value == PlaceholderValues.Body);

            if (notesBody?.TextBody is { } txBody)
            {
                txBody.RemoveAllChildren<Drawing.Paragraph>();
                txBody.Append(new Drawing.Paragraph(
                    new Drawing.Run(
                        new Drawing.RunProperties { Language = "en-US" },
                        new Drawing.Text(text))));

                notesPart.NotesSlide?.Save();
            }
        }

        private static void UpdateSlideIdentifier(SlidePart slidePart, string slideName)
        {
            var commonData = slidePart.Slide?.CommonSlideData;
            if (commonData != null)
            {
                commonData.Name = slideName;
            }
        }

        private async Task TryAttachMediaAsync(SlidePart slidePart, string mediaFileName, MediaType mediaType,
            CancellationToken ct)
        {
            try
            {
                var mediaBytes = await mediaService.LoadAsync(
                    fileName: mediaFileName,
                    ct: ct);

                if (mediaBytes == null) return;

                if (mediaType == MediaType.Audio)
                {
                    ReplaceMediaAudio(
                        slidePart: slidePart,
                        audioBytes: mediaBytes);
                }
                else
                {
                    ReplaceMediaImage(
                        slidePart: slidePart,
                        imageBytes: mediaBytes,
                        fileName: mediaFileName);
                }
            }
            catch (FileNotFoundException)
            {
                toastService.ShowError($"Media file not found: {mediaFileName}");
            }
        }

        #endregion Private Methods
    }
}