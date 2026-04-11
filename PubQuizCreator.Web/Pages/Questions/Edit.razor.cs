using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
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
        private QuestionModel model = new();
        private IBrowserFile? pendingFile;
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

        private bool CanCheck => !string.IsNullOrWhiteSpace(model.TextShort)
            || !string.IsNullOrWhiteSpace(model.Answer);

        private bool CanSave => !string.IsNullOrWhiteSpace(model.TextShort)
            && model.CategoryId.HasValue
            && !string.IsNullOrWhiteSpace(model.Answer);

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

            if (!IsNew && await QuestionService.GetAsync(Id!.Value) is { } q)
                model = QuestionModel.From(q);

            if (PreselectedCategoryId.HasValue && PreselectedCategoryId != Guid.Empty)
                model.CategoryId = PreselectedCategoryId.Value;

            if (IdeaId.HasValue && IdeaId != Guid.Empty)
            {
                var idea = await IdeaService.GetAsync(IdeaId.Value);
                if (idea != null)
                {
                    var normalized = RegexNormalized().Replace(idea.Text.Trim(), " ");
                    var match = RegexMatch().Match(normalized);
                    if (match.Success)
                    {
                        model.TextShort = match.Groups[1].Value.Trim();
                        model.Answer = match.Groups[2].Value.Trim();
                    }
                    else
                    {
                        model.TextShort = normalized;
                    }
                    model.CategoryId = idea.CategoryId;
                    ideaText = idea.Text;
                }
            }

            isInitialized = true;
        }

        protected override void OnParametersSet()
        {
            if (!isInitialized) return;

            if (PreselectedCategoryId.HasValue && PreselectedCategoryId != Guid.Empty)
                model.CategoryId = PreselectedCategoryId.Value;
        }

        #endregion Protected Methods

        #region Private Methods

        [GeneratedRegex(@"^(.+[!?])\s*(.+)$")] private static partial Regex RegexMatch();

        [GeneratedRegex(@"\s+")] private static partial Regex RegexNormalized();

        private async Task DeleteAndGoBackAsync()
        {
            var confirmed = await JS.ConfirmDeleteAsync(model.TextShort);
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
                model.IsUnusable = true;
                model.WasUsed = true;
                model.AllowReuse = false;

                var question = model.ToQuestion(Id);
                if (IsNew) await QuestionService.CreateAsync(question);
                else await QuestionService.UpdateAsync(question);

                if (IdeaId.HasValue && IdeaId != Guid.Empty)
                    await IdeaService.MarkProcessedAsync(IdeaId.Value);

                var url = IdeaId.HasValue && IdeaId != Guid.Empty
                    ? "/ideas"
                    : ReturnUrl ?? "/questions";

                Nav.NavigateTo(url);
            }
            catch (Exception ex)
            {
                model.IsUnusable = false;
                model.WasUsed = false;
                saveError = $"Error: {ex.Message}";
            }
            finally
            {
                saving = false;
            }
        }

        private async Task MarkAsUsableAsync()
        {
            model.IsUnusable = false;
            model.WasUsed = false;
            model.AllowReuse = false;

            await Task.CompletedTask;
            StateHasChanged();
        }

        private void OnCategoryChanged(ChangeEventArgs e) => model.CategoryId = Guid.TryParse(e.Value?.ToString(), out var id)
            ? id.NullIfEmpty()
            : null;

        private void OnFileSelected(InputFileChangeEventArgs e)
        {
            pendingFile = e.File;

            // Auto-detect media type from MIME type
            model.MediaType = e.File.ContentType switch
            {
                var ct when ct.StartsWith("image/") => MediaType.Image,
                var ct when ct.StartsWith("audio/") => MediaType.Audio,
                var ct when ct.StartsWith("video/") => MediaType.Video,
                _ => MediaType.None
            };
        }

        private async Task OpenAiPromptAsync()
        {
            var template = Configuration["Quiz:PromptTemplate"]
                ?? "Category: {category}\nQuestion: {question}\nAnswer: {answer}";
            var categoryName = categories.FirstOrDefault(c => c.Id == model.CategoryId)?.Name ?? "—";
            var prompt = template
                .Replace("{category}", categoryName)
                .Replace("{question}", model.TextShort)
                .Replace("{answer}", model.Answer);

            await JS.InvokeVoidAsync("navigator.clipboard.writeText", prompt);

            var aiUrl = Configuration["Quiz:AiUrl"];
            if (!string.IsNullOrWhiteSpace(aiUrl))
                await JS.InvokeVoidAsync("open", aiUrl, "_blank");
        }

        private void RemoveMediaAsync()
        {
            if (!string.IsNullOrEmpty(model.MediaFile))
                MediaService.Delete(model.MediaFile);

            model.MediaFile = null;
            model.MediaType = MediaType.None;
            pendingFile = null;
        }

        private async Task RunSimilaritySearchAsync()
        {
            var text = $"{model.TextShort} {model.Answer}".Trim();

            searchingEmbedding = true;
            await InvokeAsync(StateHasChanged);

            try
            {
                similars = await QuestionService.FindSimilarsAsync(
                    text,
                    Id ?? Guid.Empty,
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
                    if (!string.IsNullOrEmpty(model.MediaFile))
                        MediaService.Delete(model.MediaFile);

                    await using var stream = pendingFile.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024);
                    model.MediaFile = await MediaService.SaveAsync(
                        stream: stream,
                        fileName: pendingFile.Name);
                }

                var question = model.ToQuestion(Id);
                if (IsNew) await QuestionService.CreateAsync(question);
                else await QuestionService.UpdateAsync(question);

                StateService.NotifyDataChanged();

                var nextCategoryId = model.CategoryId;
                model = new QuestionModel { CategoryId = nextCategoryId };
                similars = [];
                hasSearched = false;

                if (IdeaId.HasValue && IdeaId != Guid.Empty)
                {
                    await IdeaService.MarkProcessedAsync(IdeaId.Value);
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

        private void TriggerSimilaritySearch()
        {
            debounceTimer?.Dispose();
            debounceTimer = new Timer(async _ =>
            {
                await RunSimilaritySearchAsync();
                await InvokeAsync(StateHasChanged);
            }, null, 800, Timeout.Infinite);
        }

        #endregion Private Methods

        #region Private Classes

        private sealed class QuestionModel
        {
            #region Public Properties

            public bool AllowReuse { get; set; } = false;

            public string Answer { get; set; } = "";

            public Guid? CategoryId { get; set; }

            public bool IsUnusable { get; set; } = false;

            public string? MediaFile { get; set; }

            public MediaType MediaType { get; set; } = MediaType.None;

            public string TextLong { get; set; } = "";

            public string TextShort { get; set; } = "";

            public bool WasUsed { get; set; } = false;

            #endregion Public Properties

            #region Public Methods

            public static QuestionModel From(Question q) => new()
            {
                TextShort = q.TextShort,
                TextLong = q.TextLong,
                Answer = q.Answer,
                CategoryId = q.CategoryId,
                MediaFile = q.MediaFile,
                MediaType = q.MediaType,
                IsUnusable = q.IsUnusable,
                WasUsed = q.WasUsed,
                AllowReuse = q.AllowReuse,
            };

            public Question ToQuestion(Guid? existingId) => new()
            {
                Id = existingId ?? Guid.NewGuid(),
                TextShort = TextShort,
                TextLong = TextLong,
                Answer = Answer,
                CategoryId = CategoryId,
                MediaFile = MediaFile,
                MediaType = MediaType,
                IsUnusable = IsUnusable,
                WasUsed = WasUsed,
                AllowReuse = AllowReuse,
            };

            #endregion Public Methods
        }

        #endregion Private Classes
    }
}