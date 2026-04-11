namespace PubQuizCreator.Core.Models
{
    public class RoundSlot
    {
        #region Public Properties

        public Category Category { get; set; } = null!;

        public Guid CategoryId { get; set; }

        public Guid Id { get; set; } = Guid.NewGuid();

        public int Position { get; set; }

        public Question? Question { get; set; }

        public Guid? QuestionId { get; set; }
        public Round Round { get; set; } = null!;

        public Guid RoundId { get; set; }

        #endregion Public Properties
    }
}