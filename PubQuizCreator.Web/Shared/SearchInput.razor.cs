using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared
{
    public partial class SearchInput
    {
        #region Private Fields

        private string localValue = string.Empty;
        private bool hasFocus = false;
        private ElementReference inputRef;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public string CssClass { get; set; } = "form-control form-control-sm";

        [Parameter] public string Placeholder { get; set; } = "Search…";

        [Parameter] public string Value { get; set; } = string.Empty;

        [Parameter] public EventCallback<string> ValueChanged { get; set; }

        #endregion Public Properties

        #region Public Methods

        public async Task FocusAsync() => await inputRef.FocusAsync();

        #endregion Public Methods

        #region Protected Methods

        protected override void OnParametersSet()
        {
            if (!hasFocus)
                localValue = Value;
        }

        #endregion Protected Methods

        #region Private Methods

        private void OnBlur() => hasFocus = false;

        private void OnFocus() => hasFocus = true;

        private async Task OnInput(ChangeEventArgs e)
        {
            localValue = e.Value?.ToString() ?? string.Empty;
            await ValueChanged.InvokeAsync(localValue);
        }

        #endregion Private Methods
    }
}