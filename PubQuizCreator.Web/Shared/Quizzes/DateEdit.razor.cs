using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace PubQuizCreator.Web.Shared.Quizzes
{
    public partial class DateEdit
    {
        #region Private Fields

        private string currentValue = string.Empty;
        private bool IsEditing;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public string? InputStyle { get; set; }

        [Parameter] public bool IsReadOnly { get; set; } = false;
        
        [Parameter] public string? LabelStyle { get; set; }

        [Parameter, EditorRequired] public EventCallback<DateOnly> OnConfirm { get; set; }

        [Parameter, EditorRequired] public DateOnly Value { get; set; }

        #endregion Public Properties

        #region Private Methods

        private void CancelAsync()
        {
            IsEditing = false;
        }

        private async Task HandleChange(ChangeEventArgs e)
        {
            if (DateOnly.TryParse(e.Value?.ToString(), out var parsed))
            {
                IsEditing = false;
                if (parsed != Value)
                    await OnConfirm.InvokeAsync(parsed);
            }
        }

        private void HandleKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Escape") IsEditing = false;
        }

        private void StartEditing()
        {
            if (IsReadOnly) return;

            currentValue = Value.ToString("yyyy-MM-dd");
            IsEditing = true;
        }

        #endregion Private Methods
    }
}