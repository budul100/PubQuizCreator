using Microsoft.Extensions.Configuration;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PubQuizCreator.Services
{
    public class PrintService(IConfiguration configuration)
    {
        #region Private Fields

        private readonly float fontSizeDefault = configuration.GetValue(
            key: "Print:FontSizeDefault",
            defaultValue: Constants.FontSizeDefault);

        private readonly float fontSizeHeader = configuration.GetValue(
            key: "Print:FontSizeHeader",
            defaultValue: Constants.FontSizeHeader);

        #endregion Private Fields

        #region Public Methods

        public byte[] Print(Quiz quiz)
        {
            var document = Document.Create(c => CreateQuiz(
                container: c,
                quiz: quiz));

            var result = document.GeneratePdf();

            return result;
        }

        #endregion Public Methods

        #region Private Methods

        private static void CreateFooter(RowDescriptor row, int pageNum, int pageTotal)
        {
            row.RelativeItem().Text($"Page {pageNum} of {pageTotal}");
            row.RelativeItem().AlignRight().Text("P = Picture, A = Audio, V = Video");
        }

        private static void CreateQuestions(TableDescriptor table, IEnumerable<RoundSlot> slots)
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

            var index = 0;

            foreach (var slot in slots)
            {
                index++;

                var background = index % 2 != 0
                    ? Colors.White
                    : Colors.Grey.Lighten4;

                var questions = new[] { slot.Question?.TextShort, slot.Question?.TextLong }
                    .Where(s => !string.IsNullOrWhiteSpace(s));

                var question = string.Join(
                    separator: Environment.NewLine,
                    values: questions);

                table.Cell().Background(background).Padding(5).Text(index.ToString());
                table.Cell().Background(background).Padding(5).Text(slot.Category?.Name ?? "—");
                table.Cell().Background(background).Padding(5).Text(question ?? "—");
                table.Cell().Background(background).Padding(5).Text(GetMediaCode(slot.Question?.MediaType));
                table.Cell().Background(background).BorderRight(1).BorderColor(Colors.Grey.Lighten2);
                table.Cell().Background(background).Padding(5).Text(index.ToString());
                table.Cell().Background(background).Padding(5).Text(slot.Question?.Answer ?? "—");
            }
        }

        private static string GetMediaCode(MediaType? mediaType) => mediaType switch
        {
            MediaType.Image => "P",
            MediaType.Audio => "A",
            MediaType.Video => "V",
            _ => ""
        };

        private void CreateQuiz(IDocumentContainer container, Quiz quiz)
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
                    pageTotal: pageTotal));
            }
        }

        private void CreateRound(PageDescriptor page, Quiz quiz, Round round,
            int pageNum, int pageTotal)
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

        #endregion Private Methods
    }
}