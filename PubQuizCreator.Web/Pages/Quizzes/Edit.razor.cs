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
        private List<string> availableTemplates = [];
        private List<Category> categories = [];
        private Guid dragFromRound;
        private Guid dragFromSlot;
        private Guid dragFromSlotRound;
        private RoundSlot? editCategorySlot;
        private Guid? editCategorySlotCategoryId;
        private List<Question> pickerAll = [];
        private List<Question> pickerFiltered = [];
        private RoundSlot? pickerSlot;
        private Quiz? quiz;
        private UnidirectionalInput? searchInput;
        private string searchText = string.Empty;
        private HashSet<Guid> selectedRoundIds = [];
        private Guid selectedTemplateId;
        private bool showTemplatePicker;
        private List<Template> templates = [];

        #endregion Private Fields

        #region Public Properties

        [Parameter] public Guid Id { get; set; }

        #endregion Public Properties

        #region Private Properties

        // Convenience shorthand used in guards throughout this file
        private bool IsLocked => quiz?.IsCompleted == true;

        #endregion Private Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            templates = await TemplateService.GetAllAsync();
            categories = await CategoryService.GetAllAsync();
            availableTemplates = SettingsService.GetPptxTemplateNames().ToList();

            await ReloadAsync();
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task AddEmptyRoundAsync()
        {
            if (IsLocked) return;

            await QuizService.AddEmptyRoundAsync(Id);
            await ReloadAsync();
        }

        private async Task AddRoundFromTemplateAsync()
        {
            if (IsLocked) return;

            await QuizService.AddRoundFromTemplateAsync(Id, selectedTemplateId);
            showTemplatePicker = false;
            selectedTemplateId = Guid.Empty;
            await ReloadAsync();
        }

        private void ApplyFilter()
        {
            pickerFiltered = pickerAll
                .Where(q => string.IsNullOrWhiteSpace(searchText)
                    || q.TextShort.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || q.Answer.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private async Task AssignAsync(Guid questionId)
        {
            if (IsLocked) return;
            if (pickerSlot == null) return;

            await QuizService.AssignQuestionAsync(pickerSlot.Id, questionId);

            pickerSlot = null;

            await ReloadAsync();
        }

        private async Task ConfirmAddSlotAsync()
        {
            if (IsLocked) return;
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
            if (IsLocked) return;
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
            if (IsLocked) return;
            if (dragFromSlot == Guid.Empty || dragFromSlot == dropToSlotId) return;
            if (dragFromSlotRound != roundId) return;

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
            if (IsLocked) return;

            addSlotRound = round;
            addSlotAfterPosition = afterPosition;
            addSlotCategoryId = null;
        }

        private async Task OpenPickerAsync(RoundSlot slot)
        {
            if (IsLocked) return;

            pickerSlot = slot;
            pickerAll = [];
            pickerFiltered = [];
            searchText = string.Empty;

            StateHasChanged();

            await Task.Yield();

            if (searchInput != null)
            {
                await searchInput.ClearAsync();
                await searchInput.FocusAsync();
            }

            pickerAll = await QuestionService.GetAvailableAsync(slot.CategoryId);

            ApplyFilter();
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
                    {
                        AppState.RoundsCollapsed.Add(round.Id);
                    }
                }

                // Pre-select all rounds that have slots; preserve existing selection on reload
                var roundsWithSlots = quiz.Rounds
                    .Where(r => r.Slots.Count > 0)
                    .Select(r => r.Id)
                    .ToHashSet();

                selectedRoundIds = selectedRoundIds.Count == 0
                    ? roundsWithSlots
                    : selectedRoundIds
                        .Intersect(quiz.Rounds.Select(r => r.Id))
                        .Union(roundsWithSlots
                            .Except(quiz.Rounds.Select(r => r.Id)
                                .Except(roundsWithSlots))).ToHashSet();
            }
        }

        private async Task RemoveRoundAsync(Guid roundId)
        {
            if (IsLocked) return;

            var round = quiz!.Rounds.First(r => r.Id == roundId);

            if (round.Slots.Count > 0)
            {
                ToastService.ShowError($"Round {round.Position} still has {round.Slots.Count} slot(s). Remove all slots first.");
                return;
            }

            var confirmed = await JS.ConfirmDeleteAsync($"Round {round.Position}");
            if (!confirmed) return;

            await QuizService.RemoveRoundAsync(roundId);
            selectedRoundIds.Remove(roundId);
            await ReloadAsync();
        }

        private async Task RemoveSlotAsync(Guid slotId)
        {
            if (IsLocked) return;

            var confirmed = await JS.ConfirmDeleteAsync("this slot");
            if (!confirmed) return;

            await QuizService.RemoveSlotAsync(slotId);
            await ReloadAsync();
        }

        private async Task SaveCategoryAsync(Guid slotId)
        {
            if (IsLocked) return;

            await QuizService.AssignCategoryAsync(slotId, editCategorySlotCategoryId);
            editCategorySlot = null;

            await ReloadAsync();
        }

        private async Task SaveHeaderAsync()
        {
            if (IsLocked) return;
            if (quiz == null) return;

            await QuizService.UpdatePropsAsync(quiz.Id, quiz.Title, quiz.Date);
        }

        private async Task SaveRoundTitleAsync(Round round, string? newTitle)
        {
            if (IsLocked) return;

            round.Title = newTitle;
            await QuizService.UpdateRoundTitleAsync(round.Id, newTitle);
        }

        private void SetSearch(string text)
        {
            searchText = text;
            ApplyFilter();
        }

        private void ToggleAllCollapsed()
        {
            if (quiz == null) return;

            var allCollapsed = quiz.Rounds.All(r => AppState.RoundsCollapsed.Contains(r.Id));

            if (allCollapsed)
            {
                foreach (var r in quiz.Rounds)
                    AppState.RoundsCollapsed.Remove(r.Id);
            }
            else
            {
                foreach (var r in quiz.Rounds)
                    AppState.RoundsCollapsed.Add(r.Id);
            }
        }

        private void ToggleAllRounds()
        {
            if (quiz == null) return;

            var withSlots = quiz.Rounds
                .Where(r => r.Slots.Count > 0)
                .Select(r => r.Id)
                .ToHashSet();

            if (withSlots.IsSubsetOf(selectedRoundIds))
            {
                selectedRoundIds.ExceptWith(withSlots);
            }
            else
            {
                selectedRoundIds.UnionWith(withSlots);
            }
        }

        private async Task ToggleCompletedAsync()
        {
            if (quiz == null) return;

            await QuizService.UpdateCompletedAsync(quiz.Id, !quiz.IsCompleted);
            quiz.IsCompleted = !quiz.IsCompleted;
        }

        private void ToggleRound(Guid roundId)
        {
            if (!AppState.RoundsCollapsed.Add(roundId))
                AppState.RoundsCollapsed.Remove(roundId);
        }

        private void ToggleRoundSelection(Guid roundId)
        {
            if (!selectedRoundIds.Add(roundId))
                selectedRoundIds.Remove(roundId);
        }

        private async Task UnassignAsync(Guid slotId)
        {
            if (IsLocked) return;

            await QuizService.AssignQuestionAsync(slotId, null);
            await ReloadAsync();
        }

        #endregion Private Methods
    }
}