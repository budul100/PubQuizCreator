using PubQuizCreator.Services;

namespace PubQuizCreator.Web.Shared.Main
{
    public partial class ToastVisualizer
        : IDisposable
    {
        #region Private Fields

        private CancellationTokenSource? cts;
        private bool isError;
        private string message = string.Empty;
        private bool visible;

        #endregion Private Fields

        #region Public Methods

        public void Dispose()
        {
            ToastService.OnShow -= Show;
            cts?.Cancel();
        }

        #endregion Public Methods

        #region Protected Methods

        protected override void OnInitialized()
        {
            ToastService.OnShow += Show;
        }

        #endregion Protected Methods

        #region Private Methods

        private void Hide()
        {
            visible = false;
            InvokeAsync(StateHasChanged);
        }

        private void Show(string msg, bool error)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();

            message = msg;
            isError = error;
            visible = true;
            InvokeAsync(StateHasChanged);

            // Auto-hide after 4 seconds
            _ = Task.Delay(4000, cts.Token).ContinueWith(t =>
            {
                if (!t.IsCanceled) InvokeAsync(Hide);
            });
        }

        #endregion Private Methods
    }
}