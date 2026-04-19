namespace PubQuizCreator.Core.Models
{
    public class Settings
    {
        #region Public Properties

        public List<string> AdditionalFiles { get; set; } = [];

        public string AiPrompt { get; set; } = "";

        public string AiUrl { get; set; } = "";

        public float PrintFontSizeDefault { get; set; }

        public float PrintFontSizeHeader { get; set; }

        public string TemplateAnswers { get; set; } = "";

        public string TemplateQuestions { get; set; } = "";

        public int TextShortWarnLength { get; set; }

        #endregion Public Properties
    }
}