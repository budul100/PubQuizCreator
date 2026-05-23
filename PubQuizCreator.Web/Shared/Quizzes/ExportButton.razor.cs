using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared.Quizzes
{
    public partial class ExportButton
    {
        #region Public Properties

        [Parameter] public string CssClass { get; set; } = string.Empty;

        [Parameter] public string Href { get; set; } = string.Empty;

        [Parameter] public string Label { get; set; } = string.Empty;

        #endregion Public Properties
    }
}