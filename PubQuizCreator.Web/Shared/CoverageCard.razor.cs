using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using PubQuizCreator.Services.Data;

namespace PubQuizCreator.Web.Shared
{
    public partial class CoverageCard
    {
        #region Private Fields

        private List<Coverage> coverage = [];
        private bool isLoading = true;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public CoverageMode Mode { get; set; } = CoverageMode.QuestionsToSlots;

        [Parameter] public bool NavigateToQuestions { get; set; } = false;

        [Parameter] public EventCallback<Guid> OnCategorySelected { get; set; }

        #endregion Public Properties

        #region Private Properties

        private IEnumerable<Coverage> DisplayItems => Mode switch
        {
            CoverageMode.IdeasToQuestions => coverage
                .Where(c => c.QuestionsOpen > 0)
                .OrderBy(c => c.IsCoveredIdeas)
                .ThenByDescending(c => c.IdeasOpen)
                .ThenBy(c => c.Category.Name),

            _ => coverage
                .OrderBy(c => c.IsCoveredQuestions)
                .ThenByDescending(c => c.QuestionsOpen)
                .ThenBy(c => c.Category.Name)
        };

        #endregion Private Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            coverage = await QuizService.GetCoverageAsync();

            isLoading = false;
        }

        #endregion Protected Methods
    }
}