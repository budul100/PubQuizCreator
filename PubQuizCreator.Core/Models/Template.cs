namespace PubQuizCreator.Core.Models
{
    public class Template
    {
        #region Public Properties

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public List<TemplateSlot> Slots { get; set; } = [];

        #endregion Public Properties
    }
}