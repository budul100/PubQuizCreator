namespace PubQuizCreator.Core
{
    public static class Constants
    {
        // Similarities are based on L2 (Euclidean) distance with nomic-embed-text (768 dimensions).
        // Lower = more similar. Range is roughly 0.0 (identical) to ~1.5 (unrelated).

        #region Public Fields

        // Default Ollama embedding model. Can be overridden via appsettings.json (Ollama:EmbeddingModel).
        public const string DefaultEmbeddingModel = "nomic-embed-text";

        // Default font size if not defined in app settings.
        public const float FontSizeDefault = 8f;

        // Header font size if not defined in app settings.
        public const float FontSizeHeader = 11f;

        // Number of elements shown in list views.
        public const int PageSize = 50;

        // Timeout for Ollama embedding requests (model may need a moment to load).
        public const int OllamaEmbeddingTimeoutSeconds = 10;

        // Timeout for Ollama health checks.
        public const int OllamaHealthTimeoutSeconds = 3;

        // Interval for polling Ollama availability in the top bar (seconds).
        public const int OllamaStatusPollIntervalSeconds = 30;

        // Similarity thresholds for "very similar" — likely duplicate
        public const double SimilarityThresholdHigh = 0.2;

        // Similarity thresholds for "similar" — worth checking
        public const double SimilarityThresholdMedium = 0.3;

        // Maximum recommended character length for the short question text (used in presentation).
        public const int TextShortWarnLength = 90;

        #endregion Public Fields
    }
}