using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared.Questions
{
    public partial class DeleteButton
    {
        #region Public Properties

        [Parameter] public bool IsInCompletedQuiz { get; set; }

        [Parameter] public EventCallback OnDelete { get; set; }

        [Parameter] public string? UsedInQuiz { get; set; }

        #endregion Public Properties
    }
}