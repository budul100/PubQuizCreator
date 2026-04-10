using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Services;

namespace PubQuizCreator.Web.Shared
{
    public partial class CoverageCard
    {
        #region Private Fields

        private List<Coverage> coverage = [];
        private bool isLoading = true;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public bool NavigateToQuestions { get; set; } = false;

        [Parameter] public EventCallback<Guid> OnCategorySelected { get; set; }

        #endregion Public Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            coverage = await DashboardService.GetUpcomingCoverageAsync();

            isLoading = false;
        }

        #endregion Protected Methods
    }
}