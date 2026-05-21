using Microsoft.JSInterop;

namespace PubQuizCreator.Web.Shared
{
    public partial class NavMenu
    {
        #region Private Fields

        private bool collapseNavMenu = true;

        #endregion Private Fields

        #region Private Properties

        private string? NavMenuCssClass => collapseNavMenu ? "collapse" : null;

        #endregion Private Properties

        #region Private Methods

        private async Task OnHomeClickAsync() => await JS.InvokeVoidAsync("markInternalNavigation");

        private void ToggleNavMenu() => collapseNavMenu = !collapseNavMenu;

        #endregion Private Methods
    }
}