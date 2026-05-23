using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Services;
using PubQuizCreator.Web.Helpers;
using PubQuizCreator.Web.Shared;

namespace PubQuizCreator.Web.Pages.Quizzes
{
    public partial class Edit
    {
        #region Private Fields

        private int addSlotAfterPosition;
        private Guid? addSlotCategoryId;
        private Round? addSlotRound;
        private List<Category> categories = [];
        private Guid dragFromRound;
        private Guid dragFromSlot;
        private Guid dragFromSlotRound;
        private RoundSlot? editCategorySlot;
        private Guid? editCategorySlotCategoryId;
        private List<Question> pickerAll = [];
        private List<Question> pickerFiltered = [];
        private string pickerSearch = string.Empty;
        private SearchInput? pickerSearchInput;
        private RoundSlot? pickerSlot;
        private Quiz? quiz;
        private Guid selectedTemplateId;
        private bool showTemplatePicker;
        private List<Template> templates = [];

        #endregion Private Fields

        #region Public Properties

        [Parameter] public Guid Id { get; set; }

        #endregion Public Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            templates = await TemplateService.GetAllAsync();
            categories = await CategoryService.GetAllAsync();

            await ReloadAsync();
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task AddEmptyRoundAsync()
        {
            await QuizService.AddEmptyRoundAsync(Id);
            await ReloadAsync();
        }

        private async Task AddRoundFromTemplateAsync()
        {
            await QuizService.AddRoundFromTemplateAsync(Id, selectedTemplateId);
            showTemplatePicker = false;
            selectedTemplateId = Guid.Empty;
            await ReloadAsync();
        }

        private void ApplySearch()
        {
            pickerFiltered = pickerAll
                .Where(q => string.IsNullOrWhiteSpace(pickerSearch)
                    || q.TextShort.Contains(pickerSearch, StringComparison.OrdinalIgnoreCase)
                    || q.Answer.Contains(pickerSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private async Task AssignAsync(Guid questionId)
        {
            if (pickerSlot == null) return;

            await QuizService.AssignQuestionAsync(pickerSlot.Id, questionId);

            pickerSlot = null;

            await ReloadAsync();
        }

        private async Task ConfirmAddSlotAsync()
        {
            if (addSlotRound == null) return;

            int? afterPosition = addSlotAfterPosition == 0
                ? null
                : addSlotAfterPosition;

            await QuizService.AddSlotToRoundAsync(
                addSlotRound.Id,
                addSlotCategoryId,
                afterPosition);

            addSlotRound = null;
            await ReloadAsync();
        }

        private async Task DropRound(Guid dropToId)
        {
            if (dragFromRound == Guid.Empty || dragFromRound == dropToId || quiz == null) return;

            var ordered = quiz.Rounds.OrderBy(r => r.Position).Select(r => r.Id).ToList();
            var fromIdx = ordered.IndexOf(dragFromRound);
            var toIdx = ordered.IndexOf(dropToId);
            ordered.RemoveAt(fromIdx);
            ordered.Insert(toIdx, dragFromRound);

            await QuizService.ReorderRoundsAsync(Id, ordered);
            dragFromRound = Guid.Empty;
            await ReloadAsync();
        }

        private async Task DropSlot(Guid roundId, Guid dropToSlotId)
        {
            if (dragFromSlot == Guid.Empty || dragFromSlot == dropToSlotId) return;
            if (dragFromSlotRound != roundId) return; // only within same round

            var round = quiz!.Rounds.First(r => r.Id == roundId);
            var ordered = round.Slots.OrderBy(s => s.Position).Select(s => s.Id).ToList();
            var fromIdx = ordered.IndexOf(dragFromSlot);
            var toIdx = ordered.IndexOf(dropToSlotId);
            ordered.RemoveAt(fromIdx);
            ordered.Insert(toIdx, dragFromSlot);

            await QuizService.ReorderSlotsAsync(roundId, ordered);
            dragFromSlot = Guid.Empty;
            await ReloadAsync();
        }

        private void OpenAddSlot(Round round, int afterPosition)
        {
            addSlotRound = round;
            addSlotAfterPosition = afterPosition;
            addSlotCategoryId = null;
        }

        private async Task OpenPickerAsync(RoundSlot slot)
        {
            pickerSlot = slot;
            pickerSearch = string.Empty;
            pickerAll = [];
            pickerFiltered = [];

            StateHasChanged();

            await Task.Yield(); // let Blazor render the input first

            if (pickerSearchInput != null)
            {
                await pickerSearchInput.ClearAsync();
                await pickerSearchInput.FocusAsync();
            }

            var assignedIds = quiz!.Rounds
                .SelectMany(r => r.Slots)
                .Where(s => s.QuestionId != null && s.Id != slot.Id)
                .Select(s => s.QuestionId!.Value)
                .ToHashSet();

            pickerAll = slot.CategoryId == null || slot.CategoryId == Guid.Empty
                ? await QuestionService.GetUnassignedAsync()
                : await QuestionService.GetByCategoryAsync(slot.CategoryId, assignedIds);

            ApplySearch();
        }

        private async Task ReloadAsync()
        {
            quiz = await QuizService.GetDetailAsync(Id);
            AppState.SetPageTitle(quiz?.Title ?? "Quiz");

            if (quiz != null)
            {
                foreach (var round in quiz.Rounds)
                {
                    if (AppState.RoundsKnown.Add(round.Id))
                        AppState.RoundsCollapsed.Add(round.Id);
                }
            }
        }

        private async Task RemoveRoundAsync(Guid roundId)
        {
            var round = quiz!.Rounds.First(r => r.Id == roundId);

            if (round.Slots.Count > 0)
            {
                await JS.InvokeVoidAsync("alert",
                    $"Round {round.Position} still has {round.Slots.Count} slot(s). Remove all slots first.");
                return;
            }

            var confirmed = await JS.ConfirmDeleteAsync($"Round {round.Position}");
            if (!confirmed) return;

            await QuizService.RemoveRoundAsync(roundId);
            await ReloadAsync();
        }

        private async Task RemoveSlotAsync(Guid slotId)
        {
            var confirmed = await JS.ConfirmDeleteAsync("this slot");
            if (!confirmed) return;

            await QuizService.RemoveSlotAsync(slotId);
            await ReloadAsync();
        }

        private async Task SaveCategoryAsync(Guid slotId)
        {
            await QuizService.AssignCategoryAsync(slotId, editCategorySlotCategoryId);
            editCategorySlot = null;

            await ReloadAsync();
        }

        private async Task SaveHeaderAsync()
        {
            if (quiz == null) return;
            await QuizService.UpdateAsync(quiz.Id, quiz.Title, quiz.Date);
        }

        private async Task ToggleCompletedAsync()
        {
            if (quiz == null) return;

            await QuizService.SetCompletedAsync(quiz.Id, !quiz.IsCompleted);
            quiz.IsCompleted = !quiz.IsCompleted;
        }

        private void ToggleRound(Guid roundId)
        {
            if (!AppState.RoundsCollapsed.Add(roundId))
                AppState.RoundsCollapsed.Remove(roundId);
        }

        private async Task UnassignAsync(Guid slotId)
        {
            await QuizService.AssignQuestionAsync(slotId, null);
            await ReloadAsync();
        }

        #endregion Private Methods
    }
}