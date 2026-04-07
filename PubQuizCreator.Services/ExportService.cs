using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Drawing = DocumentFormat.OpenXml.Drawing;
using Shape = DocumentFormat.OpenXml.Presentation.Shape;

namespace PubQuizCreator.Services
{
    public static class ExportService
    {
        #region Public Methods

        public static byte[] Export(IEnumerable<Dictionary<string, string>> contents,
            string sourcePath, int sourceIndex)
        {
            var destPath = Path.GetTempFileName();

            try
            {
                File.Copy(
                    sourceFileName: sourcePath,
                    destFileName: destPath,
                    overwrite: true);

                CreatePresentation(
                    contents: contents,
                    path: destPath,
                    sourceIndex: sourceIndex);

                return File.ReadAllBytes(destPath);
            }
            finally
            {
                File.Delete(destPath);
            }
        }

        #endregion Public Methods

        #region Private Methods

        private static SlidePart CloneSlide(PresentationPart presentationPart, SlidePart source)
        {
            var result = presentationPart.AddNewPart<SlidePart>();

            using var src = source.GetStream();
            using var dest = result.GetStream(FileMode.Create);

            src.CopyTo(dest);

            if (source.SlideLayoutPart != null)
            {
                result.AddPart(source.SlideLayoutPart);
            }

            return result;
        }

        private static void CreatePresentation(IEnumerable<Dictionary<string, string>> contents, string path,
            int sourceIndex)
        {
            using var doc = PresentationDocument.Open(path: path, isEditable: true);

            doc.ChangeDocumentType(PresentationDocumentType.Presentation);

            var presentation = doc.PresentationPart!.Presentation;
            var slideIdList = presentation?.SlideIdList!;
            var allSlideIds = slideIdList.Elements<SlideId>().ToList();

            var templateSlideId = allSlideIds[sourceIndex];
            var templateSlidePart = (SlidePart)doc.PresentationPart
               .GetPartById(templateSlideId.RelationshipId!);

            var insertAfter = templateSlideId.PreviousSibling<SlideId>();

            slideIdList.RemoveChild(templateSlideId);

            var nextId = allSlideIds.Max(s => s.Id!.Value) + 1;
            SlideId? lastInserted = insertAfter;

            foreach (var content in contents)
            {
                var newPart = CloneSlide(doc.PresentationPart, templateSlidePart);

                foreach (var (shapeName, text) in content)
                    SetText(newPart, shapeName, text);

                var relId = doc.PresentationPart.GetIdOfPart(newPart);
                var newSlideId = new SlideId { Id = nextId++, RelationshipId = relId };

                if (lastInserted == null)
                    slideIdList.PrependChild(newSlideId);
                else
                    slideIdList.InsertAfter(newSlideId, lastInserted);

                lastInserted = newSlideId;
            }

            doc.PresentationPart.DeletePart(templateSlidePart);
            presentation?.Save();
        }

        private static void SetText(SlidePart slidePart, string shapeName, string text)
        {
            var shape = slidePart.Slide?.Descendants<Shape>()
                .FirstOrDefault(sp => sp.NonVisualShapeProperties?
                    .NonVisualDrawingProperties?
                        .Name?.Value == shapeName);

            if (shape?.TextBody == null) return;

            var para = shape.TextBody.Elements<Drawing.Paragraph>().First();

            var rpr = para.Elements<Drawing.Run>()
                .FirstOrDefault()?
                .RunProperties?.CloneNode(deep: true) as Drawing.RunProperties;

            if (rpr != null)
            {
                rpr.Dirty = false;
                rpr.SpellingError = null;
            }

            foreach (var run in para.Elements<Drawing.Run>().ToList())
                para.RemoveChild(run);

            foreach (var endRpr in para.Elements<Drawing.EndParagraphRunProperties>().ToList())
                para.RemoveChild(endRpr);

            var newRun = new Drawing.Run();
            if (rpr != null) newRun.Append(rpr);
            newRun.Append(new Drawing.Text(text));
            para.Append(newRun);
        }

        #endregion Private Methods
    }
}