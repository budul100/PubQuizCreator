using PubQuizCreator.Core.Types;

namespace PubQuizCreator.Core.Models
{
    public sealed record QuestionRow(
        Guid Id,
        string TextShort,
        string Answer,
        Category? Category,
        bool IsUsed,
        bool AllowReuse,
        bool IsUnusable,
        MediaType MediaType,
        string? UsedInQuiz,
        DateOnly? LastUsedDate);
}