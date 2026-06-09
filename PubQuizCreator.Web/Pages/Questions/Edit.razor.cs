using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Helpers;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using PubQuizCreator.Services;
using PubQuizCreator.Web.Helpers;

namespace PubQuizCreator.Web.Pages.Questions
{
    public partial class Edit
    {
        #region Private Fields

        private List<Category> categories = [];
        private Timer? debounceTimer;
        private bool hasSearched;
        private string? ideaText;
        private bool isInitialized;
        private IBrowserFile? pendingFile;
        private Question question = new();
        private string? saveError;
        private bool saving;
        private bool searchingEmbedding;
        private List<Similar> similars = [];

        #endregion Private Fields

        #region Public Properties

        [Parameter] public Guid? Id { get; set; }

        [SupplyParameterFromQuery(Name = "ideaId")] public Guid? IdeaId { get; set; }

        [SupplyParameterFromQuery(Name = "categoryId")] public Guid? PreselectedCategoryId { get; set; }

        [SupplyParameterFromQuery(Name = "returnUrl")] public string? ReturnUrl { get; set; }

        #endregion Public Properties

        #region Private Properties

        private bool CanCheck => !string.IsNullOrWhiteSpace(question.Text)
            || !string.IsNullOrWhiteSpace(question.Answer);

        private bool CanSave => !string.IsNullOrWhiteSpace(question.Text)
            && question.CategoryId.HasValue
            && !string.IsNullOrWhiteSpace(question.Answer);

        private bool IsNew => Id == null;

        #endregion Private Properties

        #region Public Methods

        public void Dispose()
        {
            debounceTimer?.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion Public Methods

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            var title = IsNew
                ? "New Question"
                : "Edit Question";

            StateService.SetPageTitle(title);

            categories = await CategoryService.GetAllAsync();

            if (!IsNew)
            {
                var q = await QuestionService.GetAsync(Id!.Value);

                if (q != null)
                {
                    question.CategoryId = q.CategoryId;
                    question.Text = q.Text;
                    question.Answer = q.Answer;
                    question.MediaFile = q.MediaFile;
                    question.MediaType = q.MediaType;
                    question.IsUnusable = q.IsUnusable;
                    question.AllowReuse = q.AllowReuse;
                    question.Description = q.Description;
                }
            }

            if (PreselectedCategoryId.NullIfEmpty() is { } catId)
                question.CategoryId = catId;

            await ApplyIdeaAsync();

            isInitialized = true;
        }

        protected override void OnParametersSet()
        {
            if (!isInitialized) return;

            if (PreselectedCategoryId.HasValue && PreselectedCategoryId != Guid.Empty)
                question.CategoryId = PreselectedCategoryId.Value;
        }

        #endregion Protected Methods

        #region Private Methods

        [GeneratedRegex(@"^(.+[!?])\s*(.+)$")] private static partial Regex RegexMatch();

        [GeneratedRegex(@"\s+")] private static partial Regex RegexNormalized();

        private async Task ApplyIdeaAsync()
        {
            if (IdeaId.NullIfEmpty() is not { } ideaId) return;

            var idea = await IdeaService.GetAsync(ideaId);
            if (idea == null) return;

            var normalized = RegexNormalized().Replace(idea.Text.Trim(), " ");
            var match = RegexMatch().Match(normalized);

            if (match.Success)
            {
                question.Text = match.Groups[1].Value.Trim();
                question.Answer = match.Groups[2].Value.Trim();
            }
            else
            {
                question.Text = normalized;
            }

            question.CategoryId = idea.CategoryId;
            question.MediaFile = idea.MediaFile;
            question.MediaType = idea.MediaType;
            ideaText = idea.Text;
        }

        private async Task DeleteAndGoBackAsync()
        {
            var confirmed = await JS.ConfirmDeleteAsync(question.Text);
            if (!confirmed) return;

            if (Id.HasValue)
                await QuestionService.DeleteAsync(Id.Value);

            Nav.NavigateTo(ReturnUrl ?? "/questions");
        }

        private async Task MarkAsUnusableAsync()
        {
            var confirmed = await JS.ConfirmAsync(
                "Mark as unusable? The question will be saved and excluded from all lists, " +
                "but kept for duplicate detection.");
            if (!confirmed) return;

            saving = true;
            saveError = null;

            try
            {
                question.Id = Id ?? Guid.NewGuid();
                question.AllowReuse = false;
                question.IsUnusable = true;

                if (IsNew) await QuestionService.CreateAsync(question);
                else await QuestionService.UpdateAsync(question);

                if (IdeaId.HasValue && IdeaId != Guid.Empty)
                    await IdeaService.SetProcessedAsync(IdeaId.Value);

                var url = IdeaId.HasValue && IdeaId != Guid.Empty
                    ? "/ideas"
                    : ReturnUrl ?? "/questions";

                Nav.NavigateTo(url);
            }
            catch (Exception ex)
            {
                question.IsUnusable = false;
                saveError = $"Error: {ex.Message}";
            }
            finally
            {
                saving = false;
            }
        }

        private async Task MarkAsUsableAsync()
        {
            question.IsUnusable = false;
            question.AllowReuse = false;

            await Task.CompletedTask;
            StateHasChanged();
        }

        private void OnCategoryChanged(ChangeEventArgs e) => question.CategoryId = Guid.TryParse(e.Value?.ToString(), out var id)
            ? id.NullIfEmpty()
            : null;

        private void OnFileChanged((IBrowserFile File, MediaType Type) args)
        {
            pendingFile = args.File;
            question.MediaType = args.Type;
        }

        private async Task OpenAiPromptAsync()
        {
            var template = Configuration["Quiz:PromptTemplate"]
                ?? "Category: {category}\nQuestion: {question}\nAnswer: {answer}";

            var categoryName = categories
                .FirstOrDefault(c => c.Id == question.CategoryId)?.Name ?? "—";

            var prompt = template
                .Replace("{category}", categoryName)
                .Replace("{question}", question.Text)
                .Replace("{answer}", question.Answer);

            await JS.InvokeVoidAsync("navigator.clipboard.writeText", prompt);

            var aiUrl = Configuration["Quiz:AiUrl"];
            if (!string.IsNullOrWhiteSpace(aiUrl))
                await JS.InvokeVoidAsync("open", aiUrl, "_blank");
        }

        private void RemoveMediaAsync()
        {
            if (!string.IsNullOrEmpty(question.MediaFile))
            {
                MediaService.Delete(question.MediaFile);
            }

            question.MediaFile = null;
            question.MediaType = MediaType.None;
            pendingFile = null;
        }

        private async Task RunSimilaritySearchAsync()
        {
            var text = $"{question.Text} {question.Answer}".Trim();

            searchingEmbedding = true;
            await InvokeAsync(StateHasChanged);

            try
            {
                similars = await QuestionService.FindSimilarsAsync(
                    text: text,
                    excludeId: Id ?? Guid.Empty,
                    topN: 5);

                hasSearched = true;
            }
            catch
            {
                // Ollama unavailable — fail silently
            }
            finally
            {
                searchingEmbedding = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task SaveAsync()
        {
            if (!CanSave) return;

            saving = true;
            saveError = null;

            try
            {
                // Upload pending file to disk before persisting metadata
                if (pendingFile != null)
                {
                    // Delete old file if being replaced
                    if (!string.IsNullOrEmpty(question.MediaFile))
                    {
                        MediaService.Delete(question.MediaFile);
                    }

                    await using var stream = pendingFile.OpenReadStream(
                        maxAllowedSize: Constants.MaxUploadSizeBytes);

                    question.MediaFile = await MediaService.SaveAsync(
                        stream: stream,
                        fileName: pendingFile.Name);
                }

                question.Id = Id ?? Guid.NewGuid();

                if (IsNew) await QuestionService.CreateAsync(question);
                else await QuestionService.UpdateAsync(question);

                StateService.NotifyDataChanged();

                var nextCategoryId = question.CategoryId;

                question = new Question
                {
                    CategoryId = nextCategoryId
                };

                similars = [];
                hasSearched = false;

                if (IdeaId.HasValue && IdeaId != Guid.Empty)
                {
                    await IdeaService.SetProcessedAsync(IdeaId.Value);
                    Nav.NavigateTo("/ideas");
                }
                else if (IsNew)
                {
                    // Stay on new form, carry category selection over
                    var query = nextCategoryId.HasValue ? $"?categoryId={nextCategoryId}" : "";
                    Nav.NavigateTo($"/questions/new{query}");
                }
                else
                {
                    Nav.NavigateTo(ReturnUrl ?? "/questions");
                }
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

        private void SetAnswer(string text)
        {
            question.Answer = text;
            TriggerSimilaritySearch();
        }

        private void SetTextShort(string text)
        {
            question.Text = text;
            TriggerSimilaritySearch();
        }


        private void SetTextLong(string text)
        {
            question.Description = text;
            TriggerSimilaritySearch(); 
        }


        private void TriggerSimilaritySearch()
        {
            debounceTimer?.Dispose();

            debounceTimer = new Timer(
                callback: async _ =>
                {
                    await RunSimilaritySearchAsync();
                    await InvokeAsync(StateHasChanged);
                },
                state: null,
                dueTime: 800,
                period: Timeout.Infinite);
        }

        #endregion Private Methods
    }
}