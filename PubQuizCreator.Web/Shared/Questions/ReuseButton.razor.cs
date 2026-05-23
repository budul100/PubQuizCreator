using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared.Questions
{
    public partial class ReuseButton
    {
        #region Public Properties

        [Parameter] public bool AllowReuse { get; set; }

        [Parameter] public bool IsInCompletedQuiz { get; set; }

        [Parameter] public bool IsUnusable { get; set; }

        [Parameter] public EventCallback<bool> OnToggle { get; set; }

        [Parameter] public bool WasUsed { get; set; }

        #endregion Public Properties
    }
}