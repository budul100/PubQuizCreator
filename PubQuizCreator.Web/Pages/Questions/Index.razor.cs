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
        private int currentPage = 1;
        private List<QuestionRow> entries = [];
        private Guid? filterCategoryId;
        private List<QuestionRow> filtered = [];
        private CategoryFilter filterMode = CategoryFilter.All;
        private bool initialized = false;
        private bool isLoading = true;
        private Guid? lastCategoryId;
        private List<QuestionRow> paged = [];
        private Dictionary<Guid, int> questionCountByCategory = [];
        private string searchText = "";
        private bool showUsed = false;

        #endregion Private Fields

        #region Private Enums

        private enum CategoryFilter
        {
            All,
            AllIncludingHidden,
            Unusable,
            Specific
        }

        #endregion Private Enums

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

            var all = await QuestionService.GetAllAsync();
            var usageInfo = await QuestionService.GetUsageInfoMapAsync();

            entries = all.Select(q => new QuestionRow(
                q.Id,
                q.TextShort,
                q.Answer,
                q.Category,
                q.WasUsed,
                q.AllowReuse,
                q.IsUnusable,
                usageInfo.GetValueOrDefault(q.Id)?.QuizInfo,
                usageInfo.GetValueOrDefault(q.Id)?.LastUsedDate,
                usageInfo.GetValueOrDefault(q.Id)?.IsCompleted ?? false,
                q.MediaType)).ToList();

            questionCountByCategory = entries
                .Where(q => !q.IsUnusable)
                .GroupBy(q => q.Category?.Id ?? Guid.Empty)
                .ToDictionary(g => g.Key, g => g.Count());

            if (InitialCategoryId.HasValue && InitialCategoryId != Guid.Empty)
            {
                filterMode = CategoryFilter.Specific;
                filterCategoryId = InitialCategoryId.Value;
            }

            if (InitialShowUsed)
                showUsed = true;

            await ApplyFilterAsync();

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
            await ApplyFilterAsync();
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task ApplyFilterAsync()
        {
            isLoading = true;

            StateHasChanged();
            await Task.Yield();

            filtered = entries
                .Where(q => filterMode switch
                {
                    CategoryFilter.Unusable => q.IsUnusable,
                    CategoryFilter.AllIncludingHidden => !q.IsUnusable,
                    CategoryFilter.Specific => !q.IsUnusable && q.Category?.Id == filterCategoryId,
                    _ => !q.IsUnusable && q.Category?.IsHidden != true
                })
                .Where(q => filterMode == CategoryFilter.Unusable
                    || q.AllowReuse
                    || showUsed
                    || (!q.WasUsed && !q.IsInCompletedQuiz))
                .Where(q => string.IsNullOrWhiteSpace(searchText)
                    || q.TextShort.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || q.Answer.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(q => q.IsUnusable ? 1 : 0)
                .ThenBy(q => q.Category?.Name ?? "")
                .ThenBy(q => q.LastUsedDate.HasValue ? 0 : q.WasUsed ? 1 : 2)
                .ThenByDescending(q => q.LastUsedDate ?? DateOnly.MinValue)
                .ThenBy(q => q.TextShort).ToList();

            currentPage = 1;  // reset on filter change
            ApplyPaging();

            isLoading = false;
        }

        private void ApplyPaging()
        {
            paged = filtered
                .Skip((currentPage - 1) * Constants.PageSizeList)
                .Take(Constants.PageSizeList).ToList();
        }

        private async Task DeleteAsync(Guid id)
        {
            var question = entries.First(q => q.Id == id);

            var confirmed = await JS.ConfirmDeleteAsync(question.TextShort);
            if (!confirmed) return;

            await QuestionService.DeleteAsync(id);

            entries.RemoveAll(q => q.Id == id);
            questionCountByCategory = entries
                .Where(q => !q.IsUnusable)
                .GroupBy(q => q.Category?.Id ?? Guid.Empty)
                .ToDictionary(g => g.Key, g => g.Count());

            await ApplyFilterAsync();
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
            await ApplyFilterAsync();
        }

        private void OnPageChanged(int page)
        {
            currentPage = page;
            ApplyPaging();
        }

        private async Task ToggleAllowReuseAsync(Guid id, bool value)
        {
            await QuestionService.SetAllowReuseAsync(id, value);

            var entryIdx = entries.FindIndex(q => q.Id == id);
            if (entryIdx >= 0)
                entries[entryIdx] = entries[entryIdx] with { AllowReuse = value };

            var pagedIdx = paged.FindIndex(q => q.Id == id);
            if (pagedIdx >= 0)
                paged[pagedIdx] = paged[pagedIdx] with { AllowReuse = value };

            StateHasChanged();
        }

        #endregion Private Methods

        private sealed record QuestionRow(
            Guid Id,
            string TextShort,
            string Answer,
            Category? Category,
            bool WasUsed,
            bool AllowReuse,
            bool IsUnusable,
            string? UsedInQuiz,
            DateOnly? LastUsedDate,
            bool IsInCompletedQuiz,
            MediaType MediaType);
    }
}