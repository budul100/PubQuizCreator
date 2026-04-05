namespace PubQuizCreator.Core.Models
{
    public class Category
    {
        #region Public Properties

        public string ColorHex { get; set; } = "#ffffff";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid Id { get; set; } = Guid.NewGuid();

        public bool IsHidden { get; set; } = false;
        
        public string Name { get; set; } = "";

        #endregion Public Properties
    }
}