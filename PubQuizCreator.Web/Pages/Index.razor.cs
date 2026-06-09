using Microsoft.JSInterop;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Services;

namespace PubQuizCreator.Web.Pages
{
    public partial class Index
    {
        #region Private Fields

        private List<Tally>? ideas;
        private int ideasUnassigned;
        private int nextOpen;
        private Quiz? nextQuiz;
        private List<Tally>? nextTallies;
        private int nextTotal;
        private List<Tally>? questions;
        private bool isLoaded;

        #endregion Private Fields

        #region Protected Methods

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender) return;

            var isInternal = await JS.InvokeAsync<bool>("consumeInternalNavigation");
            if (isInternal) return;

            var autoOpen = await JS.InvokeAsync<bool>("getAutoOpenCapture");
            if (autoOpen)
            {
                Nav.NavigateTo("/ideas/new");
            }
        }

        protected override async Task OnInitializedAsync()
        {
            StateService.SetPageTitle("Dashboard");

            ideas = await IdeaService.GetTalliesAsync();
            ideasUnassigned = ideas.FirstOrDefault(t => t.Category == null)?.Count ?? 0;

            questions = await QuestionService.GetTalliesOpenAsync();
            //questionsTotal = await QuestionService.GetTalliesTotalAsync();

            nextQuiz = await QuizService.GetNextAsync();

            if (nextQuiz != default)
            {
                var slots = nextQuiz.Rounds.SelectMany(r => r.Slots).ToList();

                nextTotal = slots.Count;
                nextOpen = slots.Count(s => s.QuestionId == null);

                nextTallies = nextQuiz.Rounds
                    .SelectMany(r => r.Slots)
                    .GroupBy(s => s.Category)
                    .Select(g => new Tally(
                        category: g.Key,
                        count: g.Count(s => s.QuestionId == null)))
                    .OrderBy(x => x.Category?.Name)
                    .ToList();
            }

            isLoaded = true;
        }

        #endregion Protected Methods
    }
}