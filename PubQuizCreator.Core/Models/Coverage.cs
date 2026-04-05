namespace PubQuizCreator.Core.Models
{
    public class Coverage
    {
        #region Public Properties

        public int AvailableQuestions { get; set; }

        public Category Category { get; set; } = null!;

        public int Deficit => Math.Max(0, TotalOpenSlots - AvailableQuestions);

        public bool IsCovered => AvailableQuestions >= TotalOpenSlots;

        public int TotalOpenSlots { get; set; }

        #endregion Public Properties
    }
}