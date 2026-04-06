namespace PubQuizCreator.Core
{
    public static class Constants
    {
        // Similarities based on L2 distance with mxbai-embed-large (1024 dimensions).
        // Range is roughly 0.0 (identical) to ~1.1 (unrelated).

        #region Public Fields

        // Default Ollama embedding model. Can be overridden via appsettings.json (Ollama:EmbeddingModel).
        public const string DefaultEmbeddingModel = "mxbai-embed-large";

        // Number of dimensions for the embedding model. Must match the model used in Ollama and the database column type.
        public const int EmbeddingDimensions = 1024;

        // Default font size if not defined in app settings.
        public const float FontSizeDefault = 8f;

        // Header font size if not defined in app settings.
        public const float FontSizeHeader = 11f;

        // Timeout for Ollama embedding requests (model may need a moment to load).
        public const int OllamaEmbeddingTimeoutSeconds = 10;

        // Timeout for Ollama health checks.
        public const int OllamaHealthTimeoutSeconds = 3;

        // Interval for polling Ollama availability in the top bar (seconds).
        public const int OllamaStatusPollIntervalSeconds = 30;

        // Number of elements shown in list views.
        public const int PageSize = 50;

        // Similarity thresholds for "very similar" — likely duplicate
        public const double SimilarityThresholdHigh = 0.55;

        // Similarity thresholds for "similar" — worth checking
        public const double SimilarityThresholdMedium = 0.70;

        // Maximum recommended character length for the short question text (used in presentation).
        public const int TextShortWarnLength = 90;

        #endregion Public Fields
    }
}