namespace PubQuizCreator.Core.Models
{
    public record QuestionSimilar(Guid Id, string TextShort, string Answer, double Distance);
}