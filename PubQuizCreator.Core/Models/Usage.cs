namespace PubQuizCreator.Core.Models
{
    public record Usage(string QuizInfo, DateOnly LastUsedDate, bool IsCompleted);
}