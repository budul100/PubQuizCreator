using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Web.Shared
{
    public partial class QuestionSummary
    {
        #region Public Properties

        [Parameter] public string FallbackText { get; set; } = "[Click to assign]";

        [Parameter] public Question? Question { get; set; }

        #endregion Public Properties
    }
}