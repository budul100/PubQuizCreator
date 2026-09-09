using Microsoft.Extensions.Configuration;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PubQuizCreator.Services.Export
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
            var document = Document.Create(container => CreateQuiz(
                container: container,
                quiz: quiz));

            return document.GeneratePdf();
        }

        #endregion Public Methods

        #region Private Methods

        private static void CreateFooter(RowDescriptor row, int pageNum, int pageTotal)
        {
            row.RelativeItem().Text($"Page {pageNum} of {pageTotal}");
            row.RelativeItem().AlignRight().Text("P = Picture, A = Audio, V = Video");
        }

        private static void CreateHeader(IContainer cell, string title, bool center = false)
        {
            var container = cell.Padding(4);
            if (center) container = container.AlignCenter();
            container.Text(title).SemiBold();
        }

        private static void CreateRows(TableDescriptor table, IReadOnlyList<RoundSlot> slots)
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(25);    // Index
                cols.RelativeColumn(3);     // Category
                cols.RelativeColumn(10);    // Question & Description
                cols.ConstantColumn(22);    // Media Type Indicator
                cols.ConstantColumn(5);     // Divider
                cols.ConstantColumn(25);    // Index
                cols.RelativeColumn(5);     // Answer
            });

            table.Header(header =>
            {
                CreateHeader(header.Cell(), "Nr");
                CreateHeader(header.Cell(), "Category");
                CreateHeader(header.Cell(), "Question");
                CreateHeader(header.Cell(), "T", center: true);
                header.Cell();
                CreateHeader(header.Cell(), "Nr");
                CreateHeader(header.Cell(), "Answer");
            });

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var index = i + 1;

                var background = index % 2 != 0
                    ? Colors.White
                    : Colors.Grey.Lighten4;

                var questionText = FormatQuestion(
                    text: slot.Question?.Text,
                    description: slot.Question?.Description);

                var mediaCode = GetMediaCode(slot.Question?.MediaType);

                table.Cell().Background(background).Padding(5).Text(index.ToString());
                table.Cell().Background(background).Padding(5).Text(slot.Category?.Name ?? "—");
                table.Cell().Background(background).Padding(5).Text(questionText);
                table.Cell().Background(background).PaddingVertical(5).AlignCenter().Text(mediaCode);
                table.Cell().Background(background).BorderRight(1).BorderColor(Colors.Grey.Lighten2);
                table.Cell().Background(background).Padding(5).Text(index.ToString());
                table.Cell().Background(background).Padding(5).Text(slot.Question?.Answer ?? "—");
            }
        }

        private static string FormatQuestion(string? text, string? description)
        {
            var hasText = !string.IsNullOrWhiteSpace(text);
            var hasDescription = !string.IsNullOrWhiteSpace(description);

            return (hasText, hasDescription) switch
            {
                (true, true) => $"{text}{Environment.NewLine}{description}",
                (true, false) => text!,
                (false, true) => description!,
                _ => "—"
            };
        }

        private static string GetMediaCode(MediaType? mediaType) => mediaType switch
        {
            MediaType.Image => "P",
            MediaType.Audio => "A",
            MediaType.Video => "V",
            _ => string.Empty
        };

        private void CreateQuiz(IDocumentContainer container, Quiz quiz)
        {
            var rounds = quiz.Rounds
                .Where(r => r.Slots.Count > 0)
                .OrderBy(r => r.Position)
                .ToList();

            var pageTotal = rounds.Count;

            for (var pageIndex = 0; pageIndex < rounds.Count; pageIndex++)
            {
                var currentRound = rounds[pageIndex];
                var pageNum = pageIndex + 1;

                container.Page(page => CreateRound(
                    page: page,
                    quiz: quiz,
                    round: currentRound,
                    pageNum: pageNum,
                    pageTotal: pageTotal));
            }
        }

        private void CreateRound(PageDescriptor page, Quiz quiz, Round round, int pageNum, int pageTotal)
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontSize(fontSizeDefault));

            // Header
            page.Header()
                .Text($"{quiz.Title} – {quiz.Date:dd.MM.yyyy} – Round {round.Position}")
                .AlignCenter()
                .FontSize(fontSizeHeader)
                .SemiBold();

            // Content
            var slots = round.Slots
                .OrderBy(s => s.Position)
                .ToList();

            page.Content()
                .PaddingTop(8)
                .Table(table => CreateRows(
                    table: table,
                    slots: slots));

            // Footer
            page.Footer()
                .Row(row => CreateFooter(
                    row: row,
                    pageNum: pageNum,
                    pageTotal: pageTotal));
        }

        #endregion Private Methods
    }
}