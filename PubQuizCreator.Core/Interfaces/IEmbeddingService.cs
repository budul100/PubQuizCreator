namespace PubQuizCreator.Core.Interfaces
{
    public interface IEmbeddingService
    {
        #region Public Methods

        Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default);

        #endregion Public Methods
    }
}