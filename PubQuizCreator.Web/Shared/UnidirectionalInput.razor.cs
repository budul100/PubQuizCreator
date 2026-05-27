using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace PubQuizCreator.Web.Shared
{
    public partial class UnidirectionalInput
    {
        #region Private Fields

        private const int DeboundsDelayInMilliseconds = 150;

        private CancellationTokenSource? debounceCts;
        private ElementReference inputRef;
        private string localValue = string.Empty;

        #endregion Private Fields

        #region Public Properties

        [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

        [Parameter] public string CssClass { get; set; } = "form-control form-control-sm";

        [Parameter] public bool Multiline { get; set; } = false;

        [Parameter] public EventCallback<FocusEventArgs> OnInputBlur { get; set; }

        [Parameter] public EventCallback<KeyboardEventArgs> OnKeyDown { get; set; }

        [Parameter] public string Placeholder { get; set; } = "";

        [Parameter] public int Rows { get; set; } = 4;

        [Parameter] public bool ShowClearButton { get; set; } = false;

        [Parameter] public string Value { get; set; } = string.Empty;

        [Parameter] public EventCallback<string> ValueChanged { get; set; }

        #endregion Public Properties

        #region Public Methods

        public Task ClearAsync()
        {
            localValue = string.Empty;

            StateHasChanged();

            return Task.CompletedTask;
        }

        public async Task FocusAsync() => await inputRef.FocusAsync();

        #endregion Public Methods

        #region Protected Methods

        protected override void OnParametersSet()
        {
            if (Value != localValue)
            {
                localValue = Value ?? string.Empty;
            }
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task ClearAndNotifyAsync()
        {
            await ClearAsync();
            await ValueChanged.InvokeAsync(localValue);

            await FocusAsync();
        }

        private async Task HandleInput(ChangeEventArgs e)
        {
            localValue = e?.Value?.ToString() ?? string.Empty;

            debounceCts?.Cancel();
            debounceCts = new CancellationTokenSource();

            var token = debounceCts.Token;

            try
            {
                await Task.Delay(
                    millisecondsDelay: DeboundsDelayInMilliseconds,
                    cancellationToken: token);

                if (!token.IsCancellationRequested)
                {
                    await ValueChanged.InvokeAsync(localValue);
                }
            }
            catch (TaskCanceledException) { }
        }

        private async Task HandleLocalBlur(FocusEventArgs e)
        {
            var trimmed = localValue.Trim();
            localValue = trimmed;

            await ValueChanged.InvokeAsync(localValue);

            if (OnInputBlur.HasDelegate)
            {
                await OnInputBlur.InvokeAsync(e);
            }
        }

        private async Task HandleLocalKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
            {
                var trimmed = localValue.Trim();
                localValue = trimmed;

                await ValueChanged.InvokeAsync(localValue);
            }

            if (OnKeyDown.HasDelegate)
            {
                await OnKeyDown.InvokeAsync(e);
            }
        }

        #endregion Private Methods
    }
}