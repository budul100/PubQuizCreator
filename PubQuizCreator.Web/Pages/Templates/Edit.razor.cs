using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Services;

namespace PubQuizCreator.Web.Pages.Templates
{
    public partial class Edit
    {
        #region Private Fields

        private List<Category> categories = [];
        private int dragFrom = -1;
        private string? saveError;
        private bool saving;
        private List<SlotRow> slots = [];
        private Template? template;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public Guid Id { get; set; }

        #endregion Public Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            AppState.SetPageTitle("Edit Template");

            categories = await CategoryService.GetAllAsync();
            template = await TemplateService.GetAsync(Id);

            if (template == null) return;

            slots = template.Slots
                .OrderBy(s => s.Position)
                .Select(s => new SlotRow { CategoryId = s.CategoryId })
                .ToList();
        }

        #endregion Protected Methods

        #region Private Methods

        private void DropSlot(int dropTo)
        {
            if (dragFrom < 0 || dragFrom == dropTo) return;
            var item = slots[dragFrom];
            slots.RemoveAt(dragFrom);
            slots.Insert(dropTo, item);
            dragFrom = -1;
        }

        private void OnDragOver(DragEventArgs e)
        { }

        private async Task SaveAsync()
        {
            saving = true;
            saveError = null;
            try
            {
                var ids = slots
                    .Where(s => s.CategoryId != Guid.Empty)
                    .Select(s => s.CategoryId)
                    .ToList();
                await TemplateService.SaveSlotsAsync(Id, ids);
                Nav.NavigateTo("/templates");
            }
            catch (Exception ex)
            {
                saveError = $"Error: {ex.Message}";
            }
            finally
            {
                saving = false;
            }
        }

        private async Task SaveNameAsync()
        {
            if (template == null) return;
            await TemplateService.RenameAsync(Id, template.Name);
        }

        #endregion Private Methods

        #region Private Classes

        private sealed class SlotRow
        {
            #region Public Properties

            public Guid CategoryId { get; set; }

            #endregion Public Properties
        }

        #endregion Private Classes
    }
}