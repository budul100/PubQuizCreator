using Microsoft.JSInterop;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Services;

namespace PubQuizCreator.Web.Pages
{
    public partial class Index
    {
        #region Private Fields

        private DashboardStats? stats;

        #endregion Private Fields

        #region Protected Methods

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender) return;

            var width = await JS.InvokeAsync<int>("getWindowWidth");
            if (width < 641)
                Nav.NavigateTo("/ideas/new");
        }

        protected override async Task OnInitializedAsync()
        {
            StateService.SetPageTitle("Dashboard");
            stats = await DashboardService.GetStatsAsync();
        }

        #endregion Protected Methods
    }
}