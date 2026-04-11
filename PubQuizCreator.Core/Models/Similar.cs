namespace PubQuizCreator.Core.Models
{
    public record Similar(Guid Id, string TextShort, string Answer, double Distance);
}