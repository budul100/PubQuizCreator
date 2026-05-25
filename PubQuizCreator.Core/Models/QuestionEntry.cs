namespace PubQuizCreator.Core.Models
{
    public class QuestionEntry
    {
        #region Public Properties

        public bool IsUsed { get; set; }

        public DateOnly? LastUsedDate { get; set; }

        public required Question Question { get; set; }

        public string? UsedInQuiz { get; set; }

        #endregion Public Properties
    }
}