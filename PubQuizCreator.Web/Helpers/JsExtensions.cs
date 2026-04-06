using Microsoft.JSInterop;

namespace PubQuizCreator.Web.Helpers
{
    public static class JsExtensions
    {
        #region Public Methods

        public static Task AlertAsync(this IJSRuntime js, string message) => js
            .InvokeVoidAsync("alert", message).AsTask();

        public static Task<bool> ConfirmAsync(this IJSRuntime js, string message) => js
            .InvokeAsync<bool>("confirm", message).AsTask();

        public static Task<bool> ConfirmDeleteAsync(this IJSRuntime js, string name) => js
            .InvokeAsync<bool>("confirm", $"Delete \"{name}\"? This cannot be undone.").AsTask();

        #endregion Public Methods
    }
}