using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Web.Shared.Quizzes
{
    public partial class QuestionVisualizer
    {
        #region Public Properties

        [Parameter] public Question? Question { get; set; }

        #endregion Public Properties
    }
}