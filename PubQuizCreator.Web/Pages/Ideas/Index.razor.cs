using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Helpers;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Services;
using PubQuizCreator.Web.Helpers;

namespace PubQuizCreator.Web.Pages.Ideas
{
    public partial class Index
    {
        #region Private Fields

        private List<Category> categories = [];
        private int currentPage = 1;
        private List<Idea> entries = [];
        private Guid? filterCategoryId;
        private List<Idea> filtered = [];
        private IdeaCategoryFilter filterMode = IdeaCategoryFilter.All;
        private bool isLoading = true;
        private (Guid IdeaId, Guid? OldCategoryId)? lastAssignment;
        private List<Idea> paged = [];
        private string searchText = "";
        private bool sortAscending = false;
        private Timer? undoTimer;

        #endregion Private Fields

        #region Private Enums

        private enum IdeaCategoryFilter
        { All, Uncategorized, Specific }

        #endregion Private Enums

        #region Public Methods

        public void Dispose() => undoTimer?.Dispose();

        #endregion Public Methods

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            StateService.SetPageTitle("Ideas");

            searchText = StateService.IdeasSearchText;
            sortAscending = StateService.IdeasSortAscending;

            var savedId = StateService.IdeasSelectedCategory;
            if (savedId != Guid.Empty)
            {
                filterMode = IdeaCategoryFilter.Specific;
                filterCategoryId = savedId;
            }

            categories = (await CategoryService.GetAllAsync()).Where(c => !c.IsHidden).ToList();
            await ReloadAsync();

            if (filterMode == IdeaCategoryFilter.All && entries.Any(i => i.CategoryId == null))
                filterMode = IdeaCategoryFilter.Uncategorized;

            ApplyFilter();
        }

        #endregion Protected Methods

        #region Private Methods

        private void ApplyFilter()
        {
            SaveFilterState();

            var currents = entries
                .Where(i => filterMode switch
                {
                    IdeaCategoryFilter.Uncategorized => i.CategoryId == null,
                    IdeaCategoryFilter.Specific => i.CategoryId == filterCategoryId,
                    _ => true
                })
                .Where(i => string.IsNullOrWhiteSpace(searchText)
                       || i.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase));

            filtered = sortAscending
                ? currents.OrderByDescending(i => i.IsTimeSensitive).ThenBy(i => i.CreatedAt).ToList()
                : currents.OrderByDescending(i => i.IsTimeSensitive).ThenByDescending(i => i.CreatedAt).ToList();

            currentPage = 1;
            ApplyPaging();
        }

        private void ApplyPaging()
        {
            paged = filtered
                .Skip((currentPage - 1) * Constants.PageSizeList)
                .Take(Constants.PageSizeList)
                .ToList();
        }

        private async Task AssignCategoryAsync(Guid ideaId, string? categoryIdStr)
        {
            if (!Guid.TryParse(categoryIdStr, out var categoryId)) return;

            var idea = entries.First(i => i.Id == ideaId);
            lastAssignment = (ideaId, idea.CategoryId);

            await IdeaService.UpdateCategoryAsync(ideaId, categoryId.NullIfEmpty());

            idea.CategoryId = categoryId;
            idea.Category = categories.First(c => c.Id == categoryId);

            ApplyFilter();

            undoTimer?.Dispose();
            undoTimer = new Timer(async _ =>
            {
                lastAssignment = null;
                await InvokeAsync(StateHasChanged);
            }, null, 5000, Timeout.Infinite);
        }

        private async Task DeleteAsync(Guid id)
        {
            var confirmed = await JS.ConfirmAsync("Delete this idea? This cannot be undone.");
            if (!confirmed) return;

            await IdeaService.DeleteAsync(id);
            await ReloadAsync();
        }

        private string GetCategorySelectValue() => filterMode switch
        {
            IdeaCategoryFilter.Uncategorized => "uncategorized",
            IdeaCategoryFilter.Specific => filterCategoryId?.ToString() ?? "all",
            _ => "all"
        };

        private void OnCategoryChanged(ChangeEventArgs e)
        {
            var val = e.Value?.ToString();
            (filterMode, filterCategoryId) = val switch
            {
                "all" => (IdeaCategoryFilter.All, (Guid?)null),

                "uncategorized" => (IdeaCategoryFilter.Uncategorized, null),

                _ => Guid.TryParse(val, out var id)
                    ? (IdeaCategoryFilter.Specific, id)
                    : (IdeaCategoryFilter.All, null)
            };
            ApplyFilter();
        }

        private void OnCoverageFilterAsync(Guid categoryId)
        {
            filterMode = IdeaCategoryFilter.Specific;
            filterCategoryId = categoryId;
            ApplyFilter();
        }

        private void OnPageChanged(int page)
        {
            currentPage = page;
            ApplyPaging();
            StateHasChanged();
        }

        private async Task ReloadAsync()
        {
            isLoading = true;
            entries = await IdeaService.GetOpenAsync();
            isLoading = false;
            ApplyFilter();
            StateHasChanged();
        }

        private void SaveFilterState()
        {
            StateService.IdeasSearchText = searchText;
            StateService.IdeasSortAscending = sortAscending;
            StateService.IdeasSelectedCategory = filterMode == IdeaCategoryFilter.Specific
                ? filterCategoryId ?? Guid.Empty
                : Guid.Empty;
        }

        private async Task ToggleTimeSensitiveAsync(Guid id, bool value)
        {
            await IdeaService.SetTimeSensitiveAsync(id, value);
            var idea = entries.First(i => i.Id == id);
            idea.IsTimeSensitive = value;
            ApplyFilter();
        }

        private async Task UndoAssignAsync()
        {
            if (lastAssignment == null) return;
            var (ideaId, oldCategoryId) = lastAssignment.Value;

            await IdeaService.UpdateCategoryAsync(ideaId, oldCategoryId);

            var idea = entries.First(i => i.Id == ideaId);
            idea.CategoryId = oldCategoryId;
            idea.Category = oldCategoryId == null ? null : categories.FirstOrDefault(c => c.Id == oldCategoryId);

            lastAssignment = null;
            undoTimer?.Dispose();

            ApplyFilter();
        }

        #endregion Private Methods
    }
}