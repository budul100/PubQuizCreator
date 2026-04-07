using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Interfaces;

namespace PubQuizCreator.Services
{
    public class OllamaService(HttpClient httpClient, IConfiguration configuration)
        : IEmbeddingService
    {
        #region Private Fields

        private readonly string model = configuration["Ollama:EmbeddingModel"] ?? Constants.EmbeddingDefaultModel;

        #endregion Private Fields

        #region Public Methods

        public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default)
        {
            var request = new OllamaEmbedRequest { Model = model, Input = text };

            var response = await httpClient.PostAsJsonAsync("/api/embed", request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(ct)
                ?? throw new InvalidOperationException("Ollama returned an empty response.");

            return result.Embeddings[0];
        }

        #endregion Public Methods

        #region Private Classes

        private sealed class OllamaEmbedRequest
        {
            #region Public Properties

            [JsonPropertyName("input")]
            public string Input { get; set; } = "";

            [JsonPropertyName("model")]
            public string Model { get; set; } = Constants.EmbeddingDefaultModel;

            #endregion Public Properties
        }

        private sealed class OllamaEmbedResponse
        {
            #region Public Properties

            [JsonPropertyName("embeddings")]
            public float[][] Embeddings { get; set; } = [];

            #endregion Public Properties
        }

        #endregion Private Classes
    }
}