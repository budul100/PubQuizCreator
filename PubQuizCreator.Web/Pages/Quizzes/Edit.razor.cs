using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Services;

namespace PubQuizCreator.Web.Pages.Quizzes
{
    public partial class Edit
    {
        #region Private Fields

        private Guid addSlotCategoryId;
        private QuizRound? addSlotRound;
        private List<Category> categories = [];
        private Guid dragFromRound;
        private Guid dragFromSlot;
        private Guid dragFromSlotRound;
        private List<Question> pickerAll = [];
        private List<Question> pickerFiltered = [];
        private string pickerSearch = string.Empty;
        private QuizSlot? pickerSlot;
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

        private void ApplyFilter()
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
            await QuizService.AddSlotToRoundAsync(addSlotRound.Id, addSlotCategoryId);
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

        private void OnDragOver(DragEventArgs e)
        { }

        private void OpenAddSlot(QuizRound round)
        {
            addSlotRound = round;
            addSlotCategoryId = Guid.Empty;
        }

        private async Task OpenPickerAsync(QuizSlot slot)
        {
            pickerSlot = slot;
            pickerSearch = string.Empty;

            var assignedIds = quiz!.Rounds
                .SelectMany(r => r.Slots)
                .Where(s => s.QuestionId != null && s.Id != slot.Id)
                .Select(s => s.QuestionId!.Value)
                .ToHashSet();

            pickerAll = await QuestionService
                .GetByCategoryAsync(slot.CategoryId, assignedIds);

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
                        AppState.RoundsCollapsed.Add(round.Id);
                }
            }
        }

        private async Task RemoveRoundAsync(Guid roundId)
        {
            await QuizService.RemoveRoundAsync(roundId);
            await ReloadAsync();
        }

        private async Task RemoveSlotAsync(Guid slotId)
        {
            await QuizService.RemoveSlotAsync(slotId);
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