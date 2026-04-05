using NetOffice.OfficeApi.Enums;
using NetOffice.PowerPointApi;
using StringExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TextCopy;

namespace PubQuizCreator
{
    internal static class Program
    {
        #region Private Fields

        private const string FileNameQuestions = "Fragen";

        private const string GroupAnswer = "answer";
        private const string GroupQuestion = "question";
        private const string GroupType = "type";

        private const int InsertAnswersTo = 5;
        private const int InsertQestionsTo = 4;

        private const int PositionMediaLeft = 230;
        private const int PositionMediaTop = 250;

        private const string ShapesAnswer = "AntwortText";
        private const string ShapesCopyright = "FrageCopyright";
        private const string ShapesGroup = "FrageGruppe";
        private const string ShapesNumber = "FrageNummer";

        private const string ShapesPicture = "FrageBild";
        private const string ShapesQuestion = "FrageText";

        private const string SlidesAnswer = "Antwort";

        private const string SlidesQuestionsIntro1 = "Frage1";
        private const string SlidesQuestionsIntro2 = "Frage2";
        private const string SlidesQuestionsPicture = "Frage3Bild";
        private const string SlidesQuestionsRegular = "Frage3Text";
        private const string SlidesQuestionsVideo = "Frage3Video";

        private const string TypeMusic = "M";
        private const string TypePicture = "B";
        private const string TypeVideo = "V";

        #endregion Private Fields

        #region Private Methods

        private static void CreateAnswers(string fileName, IEnumerable<Match> contents)
        {
            var application = new Application();

            var presentation = application.Presentations.Open(
                fileName: fileName,
                readOnly: MsoTriState.msoFalse,
                untitled: MsoTriState.msoTrue);

            var slideAnswer = presentation.Slides[SlidesAnswer];

            var contentCount = 0;

            foreach (var content in contents)
            {
                GetSlide(
                    presentation: presentation,
                    slideName: SlidesAnswer,
                    content: content,
                    contentCount: contentCount,
                    contentPos: InsertAnswersTo + contentCount);

                contentCount++;
            }

            slideAnswer.Delete();
        }

        private static void CreateQuestions(string fileName, IEnumerable<Match> contents)
        {
            var application = new Application();

            var presentation = application.Presentations.Open(
                fileName: fileName,
                readOnly: MsoTriState.msoFalse,
                untitled: MsoTriState.msoTrue);

            var path = Path.GetDirectoryName(presentation.Path);

            var slideIntro1 = presentation.Slides[SlidesQuestionsIntro1];
            var slideIntro2 = presentation.Slides[SlidesQuestionsIntro2];
            var slideMedia = presentation.Slides[SlidesQuestionsVideo];
            var slidePicture = presentation.Slides[SlidesQuestionsPicture];
            var slideRegular = presentation.Slides[SlidesQuestionsRegular];

            var contentCount = 0;

            foreach (var content in contents)
            {
                GetSlide(
                    presentation: presentation,
                    slideName: SlidesQuestionsIntro1,
                    content: content,
                    contentCount: contentCount,
                    contentPos: InsertQestionsTo + (contentCount * 3) + 0);

                GetSlide(
                    presentation: presentation,
                    slideName: SlidesQuestionsIntro2,
                    content: content,
                    contentCount: contentCount,
                    contentPos: InsertQestionsTo + (contentCount * 3) + 1);

                var objectPath = default(string);

                if (!content.Groups[GroupType].Value.IsEmpty())
                {
                    objectPath = GetObjectPath(
                        path: path,
                        content: content,
                        contentCount: contentCount);
                }

                switch (content.Groups[GroupType].Value)
                {
                    case TypePicture:

                        var pictureSlide = GetSlide(
                            presentation: presentation,
                            slideName: SlidesQuestionsPicture,
                            content: content,
                            contentCount: contentCount,
                            contentPos: InsertQestionsTo + (contentCount * 3) + 2);

                        if (!objectPath.IsEmpty())
                        {
                            pictureSlide.Shapes[ShapesGroup]
                                .GroupItems[ShapesPicture]
                                .Fill.UserPicture(
                                    pictureFile: objectPath);
                        }

                        pictureSlide.Shapes[ShapesGroup]
                            .GroupItems[ShapesCopyright].TextFrame.TextRange.Text = "© xxx";

                        break;

                    case TypeMusic:

                        var musicSlide = GetSlide(
                            presentation: presentation,
                            slideName: SlidesQuestionsRegular,
                            content: content,
                            contentCount: contentCount,
                            contentPos: InsertQestionsTo + (contentCount * 3) + 2);

                        if (!objectPath.IsEmpty())
                        {
                            musicSlide.Shapes.AddMediaObject2(
                                fileName: objectPath,
                                linkToFile: MsoTriState.msoFalse,
                                saveWithDocument: MsoTriState.msoTrue);
                        }

                        break;

                    case TypeVideo:

                        var videoSlide = GetSlide(
                            presentation: presentation,
                            slideName: SlidesQuestionsVideo,
                            content: content,
                            contentCount: contentCount,
                            contentPos: InsertQestionsTo + (contentCount * 3) + 2);

                        if (!objectPath.IsEmpty())
                        {
                            videoSlide.Shapes.AddMediaObject2(
                                fileName: objectPath,
                                linkToFile: MsoTriState.msoFalse,
                                saveWithDocument: MsoTriState.msoTrue,
                                left: PositionMediaLeft,
                                top: PositionMediaTop);
                        }

                        break;

                    default:

                        GetSlide(
                            presentation: presentation,
                            slideName: SlidesQuestionsRegular,
                            content: content,
                            contentCount: contentCount,
                            contentPos: InsertQestionsTo + (contentCount * 3) + 2);

                        break;
                }

                contentCount++;
            }

            slideIntro1.Delete();
            slideIntro2.Delete();
            slideMedia.Delete();
            slidePicture.Delete();
            slideRegular.Delete();
        }

        private static IEnumerable<Match> GetClipboardContents()
        {
            var text = ClipboardService.GetText();

            if (!text.IsEmpty())
            {
                var regex = new Regex(@$"(?<{GroupAnswer}>.*?)\t(?<{GroupQuestion}>.*?)\t(?<{GroupType}>.*)");

                var line = default(string);

                using var reader = new StringReader(text);

                while ((line = reader.ReadLine()) != default)
                {
                    var result = regex.Match(line);

                    if (result.Success)
                    {
                        yield return result;
                    }
                }
            }
        }

        private static string GetObjectPath(string path, Match content, int contentCount)
        {
            var question = content.Groups[GroupQuestion].Value.Trim();
            var title = $"Object of question {contentCount + 1}: {question}";

            var dialog = new System.Windows.Forms.OpenFileDialog()
            {
                InitialDirectory = path,
                Title = title,
            };

            var result = dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
                ? dialog.FileName
                : default;

            return result;
        }

        private static SlideRange GetSlide(Presentation presentation, string slideName,
            Match content, int contentCount, int contentPos)
        {
            var result = default(SlideRange);

            var question = content.Groups[GroupQuestion].Value.Trim();

            if (!question.IsEmpty())
            {
                result = presentation.Slides[slideName].Duplicate();

                result.MoveTo(
                    toPos: contentPos);

                result.Shapes[ShapesNumber]
                    .TextFrame.TextRange.Text = $"Frage {contentCount + 1}";

                result.NotesPage.Shapes[2]
                    .TextFrame.TextRange.Text = question;

                if (result.Shapes.Any(s => s.Name == ShapesQuestion))
                {
                    result.Shapes[ShapesQuestion]
                        .TextFrame.TextRange.Text = question;
                }

                if (result.Shapes.Any(s => s.Name == ShapesAnswer))
                {
                    result.Shapes[ShapesAnswer]
                        .TextFrame.TextRange.Text = content.Groups[GroupAnswer].Value.Trim();
                }
            }

            return result;
        }

        [STAThread]
        private static int Main(string[] args)
        {
            if ((args?.Any() != true) || args[0].IsEmpty())
            {
                Console.WriteLine("Please define the template file to be used.");

                return 1;
            }

            var fileName = Path.GetFullPath(args[0]);

            if (!File.Exists(fileName))
            {
                Console.WriteLine($"The file {fileName} does not exist.");

                return 1;
            }

            var contents = GetClipboardContents().ToArray();

            if (contents.Length == 0)
            {
                Console.WriteLine(
                    "There is no clipboard content to be imported into the presentation. " +
                    "Please copy your questions into the clipboard.");

                return 1;
            }

            if (fileName.Contains(FileNameQuestions))
            {
                CreateQuestions(
                    fileName: fileName,
                    contents: contents);
            }
            else
            {
                CreateAnswers(
                    fileName: fileName,
                    contents: contents);
            }

            return 0;
        }

        #endregion Private Methods
    }
}