using PubQuizCreator.Core;
using PubQuizCreator.Services;

namespace PubQuizCreator.Web.Shared.Main
{
    public partial class MainLayout
    {
        #region Private Fields

        private CancellationTokenSource cts = new();
        private int missingQuestions;
        private PeriodicTimer? timer;

        #endregion Private Fields

        #region Public Methods

        public void Dispose()
        {
            StateService.OnChange -= OnStateChanged;

            cts.Cancel();
            cts.Dispose();

            timer?.Dispose();
            timer = null;
        }

        #endregion Public Methods

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            StateService.OnChange += OnStateChanged;
            await StateService.CheckOllamaAsync();
            await RefreshDeficitAsync();

            timer = new PeriodicTimer(TimeSpan.FromSeconds(Constants.OllamaStatusPollIntervalSeconds));
            _ = PollOllamaAsync();
        }

        #endregion Protected Methods

        #region Private Methods

        private void OnStateChanged() => InvokeAsync(async () => { await RefreshDeficitAsync(); StateHasChanged(); });

        private async Task PollOllamaAsync()
        {
            try
            {
                while (timer != null && await timer.WaitForNextTickAsync(cts.Token))
                {
                    await InvokeAsync(() => StateService.CheckOllamaAsync());
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on circuit teardown — swallow cleanly
            }
        }

        private async Task RefreshDeficitAsync()
        {
            var coverage = await DashboardService.GetUpcomingCoverageAsync();
            missingQuestions = coverage.Where(c => !c.IsCovered).Sum(c => c.Deficit);
        }

        #endregion Private Methods
    }
}