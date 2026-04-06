using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PubQuizCreator.Services
{
    public static class PrintService
    {
        #region Public Methods

        public static byte[] ExportQuiz(Quiz quiz, float fontSizeDefault, float fontSizeHeader)
        {
            var document = Document.Create(c => CreateQuiz(
                container: c,
                quiz: quiz,
                fontSizeDefault: fontSizeDefault,
                fontSizeHeader: fontSizeHeader));

            var result = document.GeneratePdf();

            return result;
        }

        #endregion Public Methods

        #region Private Methods

        private static void CreateFooter(RowDescriptor row, int pageNum, int pageTotal)
        {
            row.RelativeItem().Text($"Page {pageNum} of {pageTotal}");
            row.RelativeItem().AlignRight().Text("I = Image, A = Audio, V = Video");
        }

        private static void CreateQuestions(TableDescriptor table, IEnumerable<QuizSlot> slots)
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(25);    // Nr
                cols.RelativeColumn(3);     // Catg
                cols.RelativeColumn(10);    // Question
                cols.ConstantColumn(15);    // Type
                cols.ConstantColumn(5);     // Divider
                cols.ConstantColumn(25);    // Nr
                cols.RelativeColumn(5);     // Answer
            });

            table.Header(h =>
            {
                h.Cell().Padding(4).Text("Nr").SemiBold();
                h.Cell().Padding(4).Text("Category").SemiBold();
                h.Cell().Padding(4).Text("Question").SemiBold();
                h.Cell().Padding(4).Text("T").SemiBold();
                h.Cell();
                h.Cell().Padding(4).Text("Nr").SemiBold();
                h.Cell().Padding(4).Text("Answer").SemiBold();
            });

            for (var i = 0; i < slots.Count(); i++)
            {
                var slot = slots.ElementAt(i);

                var bg = i % 2 == 0
                    ? Colors.White
                    : Colors.Grey.Lighten4;

                var question = string.IsNullOrWhiteSpace(slot.Question?.TextLong)
                    ? slot.Question?.TextShort
                    : slot.Question?.TextLong;

                table.Cell().Background(bg).Padding(5).Text((i + 1).ToString());
                table.Cell().Background(bg).Padding(5).Text(slot.Category?.Name ?? "—");
                table.Cell().Background(bg).Padding(5).Text(question ?? "—");
                table.Cell().Background(bg).Padding(5).Text(GetMediaCode(slot.Question?.MediaType));
                table.Cell().Background(bg).BorderRight(1).BorderColor(Colors.Grey.Lighten2);
                table.Cell().Background(bg).Padding(5).Text((i + 1).ToString());
                table.Cell().Background(bg).Padding(5).Text(slot.Question?.Answer ?? "—");
            }
        }

        private static void CreateQuiz(IDocumentContainer container, Quiz quiz, float fontSizeDefault, float fontSizeHeader)
        {
            var rounds = quiz.Rounds
                .Where(r => r.Slots.Count > 0)
                .OrderBy(r => r.Position).ToArray();
            var pageTotal = rounds.Length;

            for (var pageIndex = 0; pageIndex < rounds.Length; pageIndex++)
            {
                container.Page(p => CreateRound(
                    page: p,
                    quiz: quiz,
                    round: rounds[pageIndex],
                    pageNum: pageIndex + 1,
                    pageTotal: pageTotal,
                    fontSizeDefault: fontSizeDefault,
                    fontSizeHeader: fontSizeHeader));
            }
        }

        private static void CreateRound(PageDescriptor page, Quiz quiz, QuizRound round,
            int pageNum, int pageTotal, float fontSizeDefault, float fontSizeHeader)
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontSize(fontSizeDefault));

            // Header
            page.Header()
                .Text($"{quiz.Title} – {quiz.Date:dd.MM.yyyy} – Round {round.Position}")
                .AlignCenter().FontSize(fontSizeHeader).SemiBold();

            // Content

            var slots = round.Slots
                .OrderBy(s => s.Position).ToArray();

            page.Content().PaddingTop(8)
                .Table(t => CreateQuestions(
                    table: t,
                    slots: slots));

            // Footer
            page.Footer()
                .Row(r => CreateFooter(
                    row: r,
                    pageNum: pageNum,
                    pageTotal: pageTotal));
        }

        private static string GetMediaCode(MediaType? mediaType) => mediaType switch
        {
            MediaType.Image => "P",
            MediaType.Audio => "A",
            MediaType.Video => "V",
            _ => ""
        };

        #endregion Private Methods
    }
}