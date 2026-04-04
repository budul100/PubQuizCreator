namespace PubQuizCreator.Core.Models
{
    public class QuizStats
    {
        #region Public Properties

        public List<CategoryStats> ByCategory { get; set; } = [];

        public int TotalOpen { get; set; }

        public int TotalSlots { get; set; }

        #endregion Public Properties
    }
}