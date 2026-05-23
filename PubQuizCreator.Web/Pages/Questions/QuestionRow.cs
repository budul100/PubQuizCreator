using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;

namespace PubQuizCreator.Web.Pages.Questions
{
    public partial class Index
    {
        private sealed record QuestionRow(
            Guid Id,
            string TextShort,
            string Answer,
            Category? Category,
            bool WasUsed,
            bool AllowReuse,
            bool IsUnusable,
            string? UsedInQuiz,
            DateOnly? LastUsedDate,
            bool IsInCompletedQuiz,
            MediaType MediaType);
    }
}