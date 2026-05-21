using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared
{
    public partial class SearchInput
    {
        #region Private Fields

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

        #region Private Methods

        private async Task OnInput(ChangeEventArgs e)
        {
            var text = e.Value?.ToString() ?? string.Empty;
            await ValueChanged.InvokeAsync(text);
        }

        #endregion Private Methods
    }
}