using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared.Quizzes
{
    public partial class ExportButton
    {
        #region Public Properties

        [Parameter, EditorRequired] public Guid QuizId { get; set; }

        [Parameter] public IEnumerable<Guid> SelectedRoundIds { get; set; } = [];

        [Parameter] public List<string> AvailableTemplates { get; set; } = [];

        #endregion Public Properties

        #region Private Properties

        private string RoundsQuery =>
            SelectedRoundIds.Any()
                ? "?rounds=" + string.Join(",", SelectedRoundIds)
                : string.Empty;

        private string PdfHref => $"/export/quiz/{QuizId}/pdf{RoundsQuery}";
        private string JsonHref => $"/export/quiz/{QuizId}/json{RoundsQuery}";

        private string PptxHref(string template) =>
            $"/export/quiz/{QuizId}/pptx{RoundsQuery}"
            + (RoundsQuery.Length > 0 ? "&" : "?")
            + $"template={Uri.EscapeDataString(template)}";

        #endregion Private Properties
    }
}
