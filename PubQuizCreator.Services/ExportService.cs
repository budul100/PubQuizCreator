using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using PubQuizCreator.Core;

namespace PubQuizCreator.Services
{
    public class ExportService(SettingsService settingsService)
    {
        // Make sure that the comments in the template slides have a placeholder text

        #region Public Fields

        public readonly string[] usedTemplates = [Constants.TemplateSlideQuestion, Constants.TemplateSlideAnswer];

        #endregion Public Fields

        #region Private Fields

        private readonly string answersPath = settingsService.GetTemplatePath("Answers");
        private readonly string questionsPath = settingsService.GetTemplatePath("Questions");

        #endregion Private Fields

        #region Public Methods

        public byte[] Export(IEnumerable<Core.Models.Slide> slides, bool isQuestions)
        {
            var destPath = System.IO.Path.GetTempFileName();

            try
            {
                var sourcePath = isQuestions
                    ? questionsPath
                    : answersPath;

                File.Copy(
                    sourceFileName: sourcePath,
                    destFileName: destPath,
                    overwrite: true);

                CreatePresentation(
                    slides: slides,
                    isQuestions: isQuestions,
                    path: destPath);

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

            using (var src = source.GetStream())

            using (var dest = result.GetStream(FileMode.Create))
                src.CopyTo(dest);

            if (source.SlideLayoutPart != null)
            {
                var layoutRelId = source.GetIdOfPart(source.SlideLayoutPart);
                result.AddPart(
                    part: source.SlideLayoutPart,
                    id: layoutRelId);
            }

            if (source.NotesSlidePart != null)
            {
                var notesRelId = source.GetIdOfPart(source.NotesSlidePart);
                var newNotesPart = result.AddNewPart<NotesSlidePart>(notesRelId);

                using (var src = source.NotesSlidePart.GetStream())

                using (var dest = newNotesPart.GetStream(FileMode.Create))
                    src.CopyTo(dest);

                if (source.NotesSlidePart.NotesMasterPart != null)
                {
                    var masterRelId = source.NotesSlidePart.GetIdOfPart(source.NotesSlidePart.NotesMasterPart);
                    newNotesPart.AddPart(
                        part: source.NotesSlidePart.NotesMasterPart,
                        id: masterRelId);
                }
            }

            return result;
        }

        private static string GetSlideName(SlidePart slidePart)
        {
            return slidePart.Slide!.CommonSlideData?.Name?.Value ?? string.Empty;
        }

        private static void SetNotes(SlidePart slidePart, string text)
        {
            var notesPart = slidePart.NotesSlidePart;
            if (notesPart == null) return;

            var notesBody = notesPart.NotesSlide!
                .Descendants<DocumentFormat.OpenXml.Presentation.Shape>()
                .FirstOrDefault(sp => sp
                    .NonVisualShapeProperties?
                    .ApplicationNonVisualDrawingProperties?
                    .GetFirstChild<PlaceholderShape>()?.Index?.Value == 1);

            if (notesBody?.TextBody == null) return;

            notesBody.TextBody.RemoveAllChildren<Paragraph>();

            var noteText = new DocumentFormat.OpenXml.Drawing.Text(text);
            var noteRun = new Run(noteText);
            var noteParagraph = new Paragraph(noteRun);
            notesBody.TextBody.AppendChild(noteParagraph);

            notesPart.NotesSlide.Save();
        }

        private static void SetText(SlidePart slidePart, string shapeName, string text)
        {
            var shape = slidePart.Slide?
                .Descendants<DocumentFormat.OpenXml.Presentation.Shape>()
                .FirstOrDefault(sp => sp.NonVisualShapeProperties?
                    .NonVisualDrawingProperties?.Name?.Value == shapeName);

            if (shape?.TextBody == null) return;

            var para = shape.TextBody.Elements<Paragraph>().First();

            var runProps = para.Elements<Run>()
                .FirstOrDefault()?
                .RunProperties?.CloneNode(deep: true) as RunProperties;

            if (runProps != null)
            {
                runProps.Dirty = false;
                runProps.SpellingError = null;
            }

            var runs = para.Elements<Run>().ToList();

            foreach (var run in runs)
            {
                para.RemoveChild(run);
            }

            var endRprs = para.Elements<EndParagraphRunProperties>().ToList();

            foreach (var endRpr in endRprs)
            {
                para.RemoveChild(endRpr);
            }

            var newRun = new Run();

            if (runProps != null)
            {
                newRun.Append(runProps);
            }

            newRun.Append(new DocumentFormat.OpenXml.Drawing.Text(text));
            para.Append(newRun);
        }

        private void CreatePresentation(IEnumerable<Core.Models.Slide> slides, bool isQuestions, string path)
        {
            using var doc = PresentationDocument.Open(path, isEditable: true);
            doc.ChangeDocumentType(PresentationDocumentType.Presentation);

            var presentation = doc.PresentationPart!.Presentation;
            var slideIdList = presentation!.SlideIdList!;
            var allSlideIds = slideIdList.Elements<SlideId>().ToList();

            var slidesByName = allSlideIds
               .Select(sid => (sid, Name: GetSlideName((SlidePart)doc.PresentationPart.GetPartById(sid.RelationshipId!))))
               .Where(t => !string.IsNullOrWhiteSpace(t.Name))
               .ToDictionary(
                   keySelector: sid => sid.Name,
                   elementSelector: sid => sid.sid);

            var templateSlideIds = usedTemplates
               .Where(slidesByName.ContainsKey)
               .ToDictionary(name => name, name => slidesByName[name]);

            var templateParts = templateSlideIds
               .ToDictionary(
                   keySelector: kv => kv.Key,
                   elementSelector: kv => (SlidePart)doc.PresentationPart.GetPartById(kv.Value.RelationshipId!));

            var firstTemplateSlideId = templateSlideIds.Values
               .OrderBy(allSlideIds.IndexOf).First();

            var insertAfter = firstTemplateSlideId.PreviousSibling<SlideId>();

            foreach (var sid in templateSlideIds.Values)
            {
                slideIdList.RemoveChild(sid);
            }

            var nextId = allSlideIds.Max(s => s.Id!.Value) + 1;
            SlideId? lastInserted = insertAfter;

            foreach (var slide in slides)
            {
                var template = isQuestions
                    ? Constants.TemplateSlideQuestion
                    : Constants.TemplateSlideAnswer;

                var templatePart = templateParts[template];
                var newPart = CloneSlide(doc.PresentationPart, templatePart);

                foreach (var shape in slide.Shapes)
                    SetText(newPart, shape.Key, shape.Value);

                if (!string.IsNullOrWhiteSpace(slide.Notes))
                    SetNotes(newPart, slide.Notes);

                newPart.Slide!.Save();

                var relId = doc.PresentationPart.GetIdOfPart(newPart);
                var newSlideId = new SlideId { Id = nextId++, RelationshipId = relId };

                if (lastInserted == null)
                    slideIdList.PrependChild(newSlideId);
                else
                    slideIdList.InsertAfter(newSlideId, lastInserted);

                lastInserted = newSlideId;
            }

            foreach (var part in templateParts.Values)
            {
                doc.PresentationPart.DeletePart(part);
            }

            presentation.Save();
        }

        #endregion Private Methods
    }
}