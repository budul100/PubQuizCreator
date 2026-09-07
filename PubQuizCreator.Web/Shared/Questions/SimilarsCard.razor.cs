using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Services.Data;

namespace PubQuizCreator.Web.Shared
{
    public partial class SimilarsCard
        : IDisposable
    {
        #region Private Fields

        private Timer? debounceTimer;
        private bool hasSearched;
        private bool isSearching;
        private string lastSearchText = string.Empty;
        private List<Similar> similars = [];

        #endregion Private Fields

        #region Public Properties

        [Parameter] public Guid ExcludeId { get; set; } = Guid.Empty;
        [Inject] public QuestionService QuestionService { get; set; } = null!;
        [Parameter] public string SearchText { get; set; } = string.Empty;

        #endregion Public Properties

        #region Public Methods

        public void Dispose()
        {
            debounceTimer?.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion Public Methods

        #region Protected Methods

        protected override void OnParametersSet()
        {
            var trimmed = SearchText?.Trim() ?? string.Empty;

            if (trimmed == lastSearchText) return;
            lastSearchText = trimmed;

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                debounceTimer?.Dispose();
                similars = [];
                hasSearched = false;
                isSearching = false;
                return;
            }

            async void SearchCallback(object? _) => await RunSearchAsync(trimmed);

            debounceTimer?.Dispose();
            debounceTimer = new Timer(
                callback: SearchCallback,
                state: null,
                dueTime: Constants.InputDebounceTime,
                period: Timeout.Infinite);
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task RunSearchAsync(string text)
        {
            isSearching = true;
            await InvokeAsync(StateHasChanged);

            try
            {
                similars = await QuestionService.FindSimilarsAsync(
                    text: text,
                    excludeId: ExcludeId,
                    topN: 5);

                hasSearched = true;
            }
            catch
            {
                // Ollama cannot be reached or is not available.
                // This is not a critical error, so we just ignore it.
            }
            finally
            {
                isSearching = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        #endregion Private Methods
    }
}