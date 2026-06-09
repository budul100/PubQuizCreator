using System.Text.Json;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Web.Helpers
{
    internal static class QuizExtensions
    {
        #region Private Fields

        private static readonly JsonSerializerOptions ExportJsonOptions = new()
        {
            WriteIndented = true
        };

        #endregion Private Fields

        #region Public Methods

        public static byte[] CreateJson(this Quiz quiz, IEnumerable<Round> rounds)
        {
            var export = new
            {
                title = quiz.Title,
                date = quiz.Date.ToString("yyyy-MM-dd"),
                rounds = rounds
                    .Select(r => new
                    {
                        position = r.Position,
                        questions = r.Slots
                            .OrderBy(s => s.Position)
                            .Select(s => new
                            {
                                position = s.Position,
                                category = s.Category?.Name,
                                text = s.Question?.Text,
                                description = s.Question?.Description,
                                answer = s.Question?.Answer,
                            })
                    })
            };

            var json = JsonSerializer.Serialize(
                value: export,
                options: ExportJsonOptions);

            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        #endregion Public Methods
    }
}