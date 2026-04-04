namespace PubQuizCreator.Core.Models
{
    public class Idea
    {
        #region Public Properties

        public Category? Category { get; set; }

        public Guid? CategoryId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid Id { get; set; } = Guid.NewGuid();

        public bool IsProcessed { get; set; } = false;

        public bool IsTimeSensitive { get; set; } = false;

        public string Text { get; set; } = "";

        #endregion Public Properties
    }
}