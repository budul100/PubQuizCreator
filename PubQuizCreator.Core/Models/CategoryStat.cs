namespace PubQuizCreator.Core.Models
{
    public class CategoryStat(Category? category, int open, int total)
    {
        #region Public Properties

        public Category? Category { get; set; } = category;

        public int Open { get; set; } = open;

        public int Total { get; set; } = total;

        #endregion Public Properties
    }
}