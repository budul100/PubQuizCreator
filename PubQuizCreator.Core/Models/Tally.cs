namespace PubQuizCreator.Core.Models
{
    public class Tally(Category? category, int count)
    {
        #region Public Properties

        public Category? Category { get; set; } = category;

        public int? Count { get; set; } = count;

        #endregion Public Properties
    }
}