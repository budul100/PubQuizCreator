using Microsoft.JSInterop;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Services;

namespace PubQuizCreator.Web.Pages
{
    public partial class Index
    {
        #region Private Fields

        private Dashboard? stats;

        #endregion Private Fields

        #region Protected Methods

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender) return;

            var autoOpen = await JS.InvokeAsync<bool>(Constants.AutoOpenGetter);

            if (autoOpen)
            {
                Nav.NavigateTo("/ideas/new");
            }
        }

        protected override async Task OnInitializedAsync()
        {
            StateService.SetPageTitle("Dashboard");
            stats = await DashboardService.GetStatsAsync();
        }

        #endregion Protected Methods
    }
}