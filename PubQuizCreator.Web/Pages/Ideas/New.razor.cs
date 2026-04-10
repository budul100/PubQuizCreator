using PubQuizCreator.Core.Helpers;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Services;

namespace PubQuizCreator.Web.Pages.Ideas
{
    public partial class New
    {
        #region Private Fields

        private List<Category> categories = [];
        private Guid categoryId;
        private bool isTimeSensitive;
        private bool saved;
        private bool saving;
        private string text = "";

        #endregion Private Fields

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            AppState.SetPageTitle("Capture Idea");
            categories = (await CategoryService.GetAllAsync()).Where(c => !c.IsHidden).ToList();
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            saving = true;
            saved = false;

            await IdeaService.CreateAsync(text.Trim(), categoryId.NullIfEmpty(), isTimeSensitive);

            text = "";
            categoryId = Guid.Empty;
            isTimeSensitive = false;
            saving = false;
            saved = true;
        }

        #endregion Private Methods
    }
}