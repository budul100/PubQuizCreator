namespace PubQuizCreator.Core.Models
{
    public class QuizRound
    {
        #region Public Properties

        public Guid Id { get; set; } = Guid.NewGuid();

        public int Position { get; set; }

        public Quiz Quiz { get; set; } = null!;

        public Guid QuizId { get; set; }

        public List<QuizSlot> Slots { get; set; } = [];

        #endregion Public Properties
    }
}