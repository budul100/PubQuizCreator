using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared
{
    public partial class UsageLabel
    {
        #region Public Properties

        [Parameter] public bool IsInCompletedQuiz { get; set; }

        [Parameter] public string? UsedInQuiz { get; set; }

        [Parameter] public bool WasUsed { get; set; }

        #endregion Public Properties
    }
}