namespace PubQuizCreator.Core.Models
{
    public class Settings
    {
        #region Public Properties

        public List<string> AdditionalFiles { get; set; } = [];

        public string AiPrompt { get; set; } = "";

        public string AiUrl { get; set; } = "";

        public List<string> PptxTemplates { get; set; } = [];

        public float PrintFontSizeDefault { get; set; }

        public float PrintFontSizeHeader { get; set; }

        public int TextShortWarnLength { get; set; }

        public string TitleFormat { get; set; } = "Question {position}";

        #endregion Public Properties
    }
}
