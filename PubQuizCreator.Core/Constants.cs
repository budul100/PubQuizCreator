namespace PubQuizCreator.Core
{
    public static class Constants
    {
        // Similarities based on L2 distance with mxbai-embed-large (1024 dimensions).
        // Range is roughly 0.0 (identical) to ~1.1 (unrelated).

        #region Public Fields

        // Default Ollama embedding model. Can be overridden via appsettings.json (Ollama:EmbeddingModel).
        public const string EmbeddingDefaultModel = "mxbai-embed-large";

        // Number of dimensions for the embedding model. Must match the model used in Ollama and the database column type.
        public const int EmbeddingDimensions = 1024;

        // Number of embeddings saved to the database in a single batch during re-embedding.
        public const int EmbeddingReEmbedSize = 50;

        // Default font size if not defined in app settings.
        public const float FontSizeDefault = 8f;

        // Header font size if not defined in app settings.
        public const float FontSizeHeader = 11f;

        // Maximum allowed file size for uploads (200 MB).
        public const long MaxUploadSizeBytes = 200 * 1024 * 1024;

        // Timeout for Ollama embedding requests (model may need a moment to load).
        public const int OllamaEmbeddingTimeoutSeconds = 10;

        // Timeout for Ollama health checks.
        public const int OllamaHealthTimeoutSeconds = 3;

        // Interval for polling Ollama availability in the top bar (seconds).
        public const int OllamaStatusPollIntervalSeconds = 30;

        // Number of elements shown in list views.
        public const int PageSizeList = 50;

        // Number of elements shown in list views.
        public const int PageSizePicker = 20;

        // Similarity thresholds for "very similar" — likely duplicate
        public const double SimilarityThresholdHigh = 0.55;

        // Similarity thresholds for "similar" — worth checking
        public const double SimilarityThresholdMedium = 0.70;

        // Name of the answer shape
        public const string TemplateShapeAnswer = "Answer";

        // Name of the content shape
        public const string TemplateShapeMedia = "Media";

        // Name of the question shape
        public const string TemplateShapeQuestion = "Question";

        // Name of the title shape
        public const string TemplateShapeTitle = "Title";

        // Name of the answer template slide
        public const string TemplateSlideAnswer = "Answer";

        // Name of the question template slide
        public const string TemplateSlideContent = "Content";

        // Name of the question template slide
        public const string TemplateSlideQuestion = "Question";

        // Maximum recommended character length for the short question text (used in presentation).
        public const int TextShortWarnLength = 90;

        #endregion Public Fields
    }
}