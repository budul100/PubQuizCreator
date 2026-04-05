namespace PubQuizCreator.Core.Models
{
    public class QuestionStats
    {
        #region Public Properties

        public int AvailableQuestions { get; set; }

        public Category Category { get; set; } = null!;

        #endregion Public Properties
    }
}