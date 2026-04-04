namespace PubQuizCreator.Core.Models
{
    public class CategoryStats
    {
        #region Public Properties

        public Category Category { get; set; } = null!;

        public int OpenSlots { get; set; }

        public int TotalSlots { get; set; }

        #endregion Public Properties
    }
}