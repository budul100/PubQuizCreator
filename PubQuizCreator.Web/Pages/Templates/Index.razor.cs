using PubQuizCreator.Services;
using PubQuizCreator.Web.Helpers;

namespace PubQuizCreator.Web.Pages.Templates
{
    public partial class Index
    {
        #region Private Fields

        private string newName = "New Template";
        private bool showCreate;
        private List<Core.Models.Template> templates = [];

        #endregion Private Fields

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            AppState.SetPageTitle("Templates");
            templates = await TemplateService.GetAllAsync();
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task CreateAsync()
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            var template = await TemplateService.CreateAsync(newName.Trim());
            Nav.NavigateTo($"/templates/{template.Id}/edit");
        }

        private async Task DeleteAsync(Guid id)
        {
            var template = templates.First(x => x.Id == id);

            var confirmed = await JS.ConfirmDeleteAsync(template.Name);
            if (!confirmed) return;

            await TemplateService.DeleteAsync(id);
            templates = await TemplateService.GetAllAsync();
        }

        private async Task DuplicateAsync(Guid id)
        {
            var copy = await TemplateService.DuplicateAsync(id);
            Nav.NavigateTo($"/templates/{copy.Id}/edit");
        }

        #endregion Private Methods
    }
}