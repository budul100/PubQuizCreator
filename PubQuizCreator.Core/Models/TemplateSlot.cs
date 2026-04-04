namespace PubQuizCreator.Core.Models
{
    public class TemplateSlot
    {
        #region Public Properties

        public Category Category { get; set; } = null!;

        public Guid CategoryId { get; set; }

        public Guid Id { get; set; } = Guid.NewGuid();

        public int Position { get; set; }

        public Template Template { get; set; } = null!;

        public Guid TemplateId { get; set; }

        #endregion Public Properties
    }
}