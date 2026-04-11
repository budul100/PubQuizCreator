namespace PubQuizCreator.Core.Models
{
    public class Quiz
    {
        #region Public Properties

        public DateOnly Date { get; set; }

        public Guid Id { get; set; } = Guid.NewGuid();

        public bool IsCompleted { get; set; } = false;

        public List<Round> Rounds { get; set; } = [];

        public string Title { get; set; } = string.Empty;

        #endregion Public Properties
    }
}