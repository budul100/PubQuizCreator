namespace PubQuizCreator.Core.Models
{
    public class QuizSlot
    {
        #region Public Properties

        public Category Category { get; set; } = null!;

        public Guid CategoryId { get; set; }

        public Guid Id { get; set; } = Guid.NewGuid();

        public int Position { get; set; }

        public Question? Question { get; set; }

        public Guid? QuestionId { get; set; }

        public Guid QuizRoundId { get; set; }

        public QuizRound Round { get; set; } = null!;

        #endregion Public Properties
    }
}