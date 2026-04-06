using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PubQuizCreator.Services
{
    public class PrintService
    {
        #region Public Methods

        public static byte[] ExportQuiz(Quiz quiz)
        {
            var rounds = quiz.Rounds.OrderBy(r => r.Position).ToList();
            var totalPages = rounds.Count;

            return Document.Create(container =>
            {
                for (var pageIndex = 0; pageIndex < rounds.Count; pageIndex++)
                {
                    var round = rounds[pageIndex];
                    var slots = round.Slots.OrderBy(s => s.Position).ToList();

                    if (slots.Count == 0) continue; // skip empty rounds

                    var pageNum = pageIndex + 1;

                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(9));

                        // Header
                        page.Header().Text($"{quiz.Title} – {quiz.Date:dd.MM.yyyy} – Round {round.Position}")
                            .FontSize(11).SemiBold();

                        // Content
                        page.Content().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(30);   // Nr
                                cols.RelativeColumn(1);   // Catg
                                cols.RelativeColumn(4);    // Question
                                cols.ConstantColumn(30);   // Type
                                cols.ConstantColumn(10);   // Divider
                                cols.ConstantColumn(30);   // Nr (answer)
                                cols.RelativeColumn(3);    // Answer
                            });

                            table.Header(h =>
                            {
                                h.Cell().Padding(4).Text("Nr").SemiBold();
                                h.Cell().Padding(4).Text("Category").SemiBold();
                                h.Cell().Padding(4).Text("Question").SemiBold();
                                h.Cell().Padding(4).Text("Type").SemiBold();
                                h.Cell();
                                h.Cell().Padding(4).Text("Nr").SemiBold();
                                h.Cell().Padding(4).Text("Answer").SemiBold();
                            });

                            for (var i = 0; i < slots.Count; i++)
                            {
                                var slot = slots[i];
                                var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                table.Cell().Background(bg).Padding(5).Text((i + 1).ToString());
                                table.Cell().Background(bg).Padding(5).Text(slot.Category?.Name ?? "—");
                                table.Cell().Background(bg).Padding(5).Text(slot.Question?.TextShort ?? "—");
                                table.Cell().Background(bg).Padding(5).Text(GetMediaCode(slot.Question?.MediaType));
                                table.Cell().Background(bg).BorderRight(1).BorderColor(Colors.Grey.Lighten2);
                                table.Cell().Background(bg).Padding(5).Text((i + 1).ToString());
                                table.Cell().Background(bg).Padding(5).Text(slot.Question?.Answer ?? "—");
                            }
                        });

                        // Footer
                        page.Footer().Row(row =>
                        {
                            row.RelativeItem().Text($"Page {pageNum} of {totalPages}");
                            row.RelativeItem().AlignRight().Text("I = Image, A = Audio, V = Video");
                        });
                    });
                }
            }).GeneratePdf();
        }

        #endregion Public Methods

        #region Private Methods

        private static string GetMediaCode(MediaType? mediaType) => mediaType switch
        {
            MediaType.Image => "I",
            MediaType.Audio => "A",
            MediaType.Video => "V",
            _ => ""
        };

        #endregion Private Methods
    }
}