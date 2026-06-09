using PubQuizCreator.Core.Models;
using PubQuizCreator.Web.Helpers;

namespace PubQuizCreator.Web.Pages.Quizzes
{
    public partial class Index
    {
        #region Private Fields

        private const string newTitle = "New Quiz";

        private List<Quiz> active = [];
        private List<Quiz> completed = [];
        private DateOnly newDate = DateOnly.FromDateTime(DateTime.Today);
        private bool showCompleted = false;
        private bool showCreate;

        #endregion Private Fields

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            StateService.SetPageTitle("Quizzes");
            await ReloadAsync();
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task CreateAsync()
        {
            var quiz = await QuizService.CreateAsync(newTitle, newDate);
            Nav.NavigateTo($"/quizzes/{quiz.Id}");
        }

        private async Task DeleteAsync(Guid id)
        {
            var list = showCompleted
                ? completed
                : active;

            var quiz = list.First(q => q.Id == id);

            var confirmed = await JS.ConfirmDeleteAsync(quiz.Title);
            if (!confirmed) return;

            await QuizService.DeleteAsync(id);
            await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            active = await QuizService.GetActiveAsync();
            completed = await QuizService.GetCompletedAsync();
        }

        private void SetView(bool completed) => showCompleted = completed;

        #endregion Private Methods
    }
}