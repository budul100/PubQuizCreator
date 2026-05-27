using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using PubQuizCreator.Services.StateService.Models;

namespace PubQuizCreator.Services.StateService
{
    public class StateService(IHttpClientFactory httpClientFactory, IConfiguration configuration, 
        ToastService toastService)
    {
        #region Private Fields

        private readonly string embeddingModel = configuration["Ollama:EmbeddingModel"] ?? "";

        private readonly HttpClient httpClient = httpClientFactory.CreateClient("OllamaHealth");

        #endregion Private Fields

        #region Public Events

        public event Action? OnChange;

        #endregion Public Events

        #region Public Properties

        public string IdeasSearchText { get; set; } = "";

        public Guid IdeasSelectedCategory { get; set; } = Guid.Empty;

        public bool IdeasSortAscending { get; set; } = false;

        public bool OllamaOnline { get; private set; }

        public string PageTitle { get; private set; } = "";

        public HashSet<Guid> RoundsCollapsed { get; } = [];

        public HashSet<Guid> RoundsKnown { get; } = [];

        #endregion Public Properties

        #region Public Methods

        public async Task CheckOllamaAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await httpClient.GetAsync("/api/tags", ct);
                OllamaOnline = response.IsSuccessStatusCode;

                if (OllamaOnline && !string.IsNullOrEmpty(embeddingModel))
                {
                    var json = await response.Content.ReadFromJsonAsync<TagsResponse>(cancellationToken: ct);
                    var modelFound = json?.Models?.Any(m =>
                        m.Name.StartsWith(embeddingModel, StringComparison.OrdinalIgnoreCase)) ?? false;

                    if (!modelFound)
                    {
                        OllamaOnline = false;
                        toastService.ShowError(
                            $"Ollama is running, but embedding model \"{embeddingModel}\" is not available.");
                    }
                }
            }
            catch
            {
                OllamaOnline = false;
            }

            OnChange?.Invoke();
        }

        public void NotifyDataChanged() => OnChange?.Invoke();

        public void SetPageTitle(string title)
        {
            PageTitle = title;
            OnChange?.Invoke();
        }

        #endregion Public Methods
    }
}