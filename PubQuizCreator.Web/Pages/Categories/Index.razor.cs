using PubQuizCreator.Core.Models;
using PubQuizCreator.Services;
using PubQuizCreator.Web.Helpers;

namespace PubQuizCreator.Web.Pages.Categories
{
    public partial class Index
    {
        #region Private Fields

        private List<Category> categories = [];
        private string newColor = "#95a5a6";
        private string newName = "";
        private Dictionary<Guid, int> questionCounts = [];
        private bool showCreate;

        #endregion Private Fields

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            AppState.SetPageTitle("Categories");
            await ReloadAsync();
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task CreateAsync()
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            await CategoryService.CreateAsync(newName.Trim(), newColor);
            newName = "";
            newColor = "#95a5a6";
            showCreate = false;
            await ReloadAsync();
        }

        private async Task DeleteAsync(Guid id)
        {
            var cat = categories.First(c => c.Id == id);

            if (await CategoryService.IsInUseAsync(id))
            {
                ToastService.ShowError($"\"{cat.Name}\" is in use and cannot be deleted.");
                return;
            }

            var confirmed = await JS.ConfirmDeleteAsync(cat.Name);
            if (!confirmed) return;

            await CategoryService.DeleteAsync(id);
            await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            categories = await CategoryService.GetAllAsync();
            questionCounts = await CategoryService.GetQuestionCountsAsync();
        }

        private async Task UpdateColorAsync(Category cat, string color)
        {
            cat.ColorHex = color;
            await CategoryService.UpdateAsync(cat.Id, cat.Name, cat.ColorHex, cat.IsHidden);
            await ReloadAsync();
        }

        private async Task UpdateHiddenAsync(Category cat, bool isHidden)
        {
            cat.IsHidden = isHidden;
            await CategoryService.UpdateAsync(cat.Id, cat.Name, cat.ColorHex, cat.IsHidden);
        }

        private async Task UpdateNameAsync(Category cat, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == cat.Name) return;
            cat.Name = name;
            await CategoryService.UpdateAsync(cat.Id, cat.Name, cat.ColorHex, cat.IsHidden);
        }

        #endregion Private Methods
    }
}