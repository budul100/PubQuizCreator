namespace PubQuizCreator.Core.Models
{
    public class Dashboard
    {
        #region Public Properties

        public List<Count> IdeasByCategory { get; set; } = [];

        public int IdeasTotal { get; set; }

        public int IdeasWithoutCategory { get; set; }

        public Quiz? NextQuiz { get; set; }

        public int NextQuizOpenSlots { get; set; }

        public int NextQuizTotalSlots { get; set; }

        public List<Count> QuestionsByCategory { get; set; } = [];

        #endregion Public Properties
    }
}