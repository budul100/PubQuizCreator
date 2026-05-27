using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace PubQuizCreator.Web.Shared
{
    public partial class UnidirectionalInput
    {
        #region Private Fields

        private bool forceRender = false;
        private ElementReference inputRef;
        private bool isInitialized = false;
        private bool isTyping;
        private string? lastValue;
        private string localValue = string.Empty;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public string CssClass { get; set; } = "form-control form-control-sm";

        [Parameter] public bool Multiline { get; set; } = false;

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
            lastValue = string.Empty;

            forceRender = true;
            StateHasChanged();

            return Task.CompletedTask;
        }

        public async Task FocusAsync() => await inputRef.FocusAsync();

        #endregion Public Methods

        #region Protected Methods

        protected override void OnParametersSet()
        {
            if (Value != lastValue)
            {
                lastValue = Value;

                if (!isTyping || !isInitialized)
                {
                    localValue = Value;
                    isInitialized = true;

                    forceRender = true;
                    StateHasChanged();
                }
            }
        }

        protected override bool ShouldRender()
        {
            if (forceRender)
            {
                forceRender = false;
                return true;
            }

            return false;
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task ClearAndNotifyAsync()
        {
            await ClearAsync();
            await ValueChanged.InvokeAsync(localValue);

            await FocusAsync();
        }

        private async Task HandleBlur()
        {
            await TrimAndPropagateAsync();
        }

        private async Task HandleInput(ChangeEventArgs e)
        {
            isTyping = true;

            localValue = e?.Value?.ToString() ?? string.Empty;
            await ValueChanged.InvokeAsync(localValue);

            isTyping = false;
        }

        private async Task HandleKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
            {
                await TrimAndPropagateAsync();
            }
        }

        private async Task TrimAndPropagateAsync()
        {
            var trimmed = localValue?.Trim();

            if (localValue != trimmed)
            {
                localValue = trimmed ?? string.Empty;
                lastValue = trimmed;

                forceRender = true;
                await ValueChanged.InvokeAsync(localValue);
            }
        }

        #endregion Private Methods
    }
}