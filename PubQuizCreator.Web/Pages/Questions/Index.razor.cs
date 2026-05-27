using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using PubQuizCreator.Services;
using PubQuizCreator.Web.Helpers;

namespace PubQuizCreator.Web.Pages.Questions
{
    public partial class Index
    {
        #region Private Fields

        private List<Category> categories = [];
        private Dictionary<Guid, int> countByCategory = [];
        private int currentPage = 1;
        private List<QuestionEntry> entries = [];
        private Guid? filterCategoryId;
        private CategoryFilter filterMode = CategoryFilter.All;
        private bool initialized = false;
        private bool isLoading = true;
        private Guid? lastCategoryId;
        private string searchText = "";
        private bool showUsed = false;

        private int totalCount = 0;

        #endregion Private Fields

        #region Public Properties

        [SupplyParameterFromQuery(Name = "categoryId")] public Guid? InitialCategoryId { get; set; }

        [SupplyParameterFromQuery(Name = "showUsed")] public bool InitialShowUsed { get; set; }

        #endregion Public Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            StateService.SetPageTitle("Questions");
            isLoading = true;

            categories = await CategoryService.GetAllAsync();
            countByCategory = await QuestionService.GetCountByCategoryAsync(); // see below

            if (InitialCategoryId.HasValue && InitialCategoryId != Guid.Empty)
            {
                filterMode = CategoryFilter.Specific;
                filterCategoryId = InitialCategoryId.Value;
            }

            if (InitialShowUsed)
                showUsed = true;

            await ReloadAsync();

            isLoading = false;
            lastCategoryId = InitialCategoryId;
            initialized = true;
        }

        protected override async Task OnParametersSetAsync()
        {
            if (!initialized) return;
            if (InitialCategoryId == lastCategoryId) return;

            lastCategoryId = InitialCategoryId;

            if (InitialCategoryId.HasValue && InitialCategoryId != Guid.Empty)
            {
                filterMode = CategoryFilter.Specific;
                filterCategoryId = InitialCategoryId.Value;
            }
            else
            {
                filterMode = CategoryFilter.All;
                filterCategoryId = null;
            }

            showUsed = InitialShowUsed;

            await ReloadAsync();
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task ApplyFilterAsync()
        {
            currentPage = 1;
            await ReloadAsync();
        }

        private async Task DeleteAsync(Guid id)
        {
            var current = entries.First(q => q.Question.Id == id);

            var confirmed = await JS.ConfirmDeleteAsync(current.Question.TextShort);
            if (!confirmed) return;

            MediaService.Delete(current.Question.MediaFile);
            await QuestionService.DeleteAsync(id);

            entries.RemoveAll(q => q.Question.Id == id);
            countByCategory = await QuestionService.GetCountByCategoryAsync();

            await ReloadAsync();
        }

        private string GetCategorySelectValue() => filterMode switch
        {
            CategoryFilter.AllIncludingHidden => "allWithHidden",

            CategoryFilter.Unusable => "unusable",

            CategoryFilter.Specific => filterCategoryId?.ToString() ?? "all",

            _ => "all"
        };

        private async Task OnCategoryChanged(ChangeEventArgs e)
        {
            var val = e.Value?.ToString();

            (filterMode, filterCategoryId) = val switch
            {
                "all" => (CategoryFilter.All, (Guid?)null),

                "allWithHidden" => (CategoryFilter.AllIncludingHidden, null),

                "unusable" => (CategoryFilter.Unusable, null),

                _ => Guid.TryParse(val, out var id)
                    ? (CategoryFilter.Specific, id)
                    : (CategoryFilter.All, null)
            };

            await ReloadAsync();
        }

        private async Task OnPageChanged(int page)
        {
            currentPage = page;
            await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            isLoading = true;
            StateHasChanged();
            await Task.Yield();

            (entries, totalCount) = await QuestionService.GetPagedAsync(
                page: currentPage,
                pageSize: Constants.PageSizeList,
                filterMode: filterMode,
                categoryId: filterCategoryId,
                showUsed: showUsed,
                search: searchText);

            isLoading = false;
        }

        private async Task SetSearchAsync(string text)
        {
            searchText = text;
            await ApplyFilterAsync();
        }

        private async Task ToggleReuseAsync(Guid id, bool value)
        {
            await QuestionService.UpdateReuseAsync(id, value);

            var index = entries.FindIndex(q => q.Question.Id == id);

            if (index >= 0)
            {
                var question = entries[index].Question;
                question.AllowReuse = value;
                entries[index].Question = question;
            }

            StateHasChanged();
        }

        #endregion Private Methods
    }
}