using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared.Questions
{
    public partial class UsageLabel
    {
        #region Public Properties

        [Parameter] public bool IsUsed { get; set; }

        [Parameter] public string? UsedInQuiz { get; set; }

        #endregion Public Properties
    }
}