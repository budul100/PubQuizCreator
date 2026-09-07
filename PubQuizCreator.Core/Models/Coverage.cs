namespace PubQuizCreator.Core.Models
{
    public class Coverage
    {
        #region Public Properties

        public Category Category { get; set; } = null!;

        public int IdeasAvailable { get; set; }

        public int IdeasOpen => Math.Max(0, QuestionsOpen - IdeasAvailable);

        public bool IsCoveredIdeas => IdeasAvailable >= QuestionsOpen;

        public bool IsCoveredQuestions => QuestionsAvailable >= SlotsOpen;

        public int QuestionsAvailable { get; set; }

        public int QuestionsOpen => Math.Max(0, SlotsOpen - QuestionsAvailable);

        public int SlotsOpen { get; set; }

        #endregion Public Properties
    }
}