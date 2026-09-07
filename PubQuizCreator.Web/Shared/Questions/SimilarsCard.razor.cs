using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Services.Data;

namespace PubQuizCreator.Web.Shared.Questions
{
    public partial class SimilarsCard
        : IDisposable
    {
        #region Private Fields

        private Timer? debounceTimer;
        private bool hasSearched;
        private bool isSearching;
        private string lastAnswer = string.Empty;
        private string lastQuestion = string.Empty;
        private List<Similar> similars = [];

        #endregion Private Fields

        #region Public Properties

        [Parameter] public string AnswerText { get; set; } = string.Empty;

        [Parameter] public Guid ExcludeId { get; set; } = Guid.Empty;

        [Inject] public QuestionService QuestionService { get; set; } = null!;

        [Parameter] public string QuestionText { get; set; } = string.Empty;

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
            var q = QuestionText?.Trim() ?? string.Empty;
            var a = AnswerText?.Trim() ?? string.Empty;

            // Only trigger if one of the two fields has actually changed
            if (q == lastQuestion
                && a == lastAnswer) return;

            lastQuestion = q;
            lastAnswer = a;

            // Minimum length for meaningful vector search (at least 10 characters in the question or answer)
            if (q.Length < Constants.SimilaritySearchLengthMin
                && $"{q} {a}".Trim().Length < Constants.SimilaritySearchLengthMin)
            {
                debounceTimer?.Dispose();
                similars = [];
                hasSearched = false;
                isSearching = false;
                return;
            }

            debounceTimer?.Dispose();
            debounceTimer = new Timer(
                callback: async _ => await RunSearchAsync(q, a),
                state: null,
                dueTime: Constants.InputDebounceTime,
                period: Timeout.Infinite);
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task RunSearchAsync(string question, string answer)
        {
            isSearching = true;
            await InvokeAsync(StateHasChanged);

            try
            {
                var combined = $"{question} {answer}".Trim();

                similars = await QuestionService.FindSimilarsAsync(
                    text1: question,
                    text2: combined,
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