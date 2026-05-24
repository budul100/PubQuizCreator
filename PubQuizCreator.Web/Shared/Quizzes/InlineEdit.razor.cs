using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace PubQuizCreator.Web.Shared.Quizzes
{
    public partial class InlineEdit
    {
        #region Private Fields

        private string currentValue = string.Empty;
        private string displayValue = string.Empty;
        private bool IsEditing;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public string ConfirmLabel { get; set; } = "Save";

        [Parameter] public string? InputStyle { get; set; }

        [Parameter] public string? LabelStyle { get; set; }

        [Parameter] public EventCallback OnCancel { get; set; }

        [Parameter, EditorRequired] public EventCallback<string?> OnConfirm { get; set; }

        [Parameter] public string? Placeholder { get; set; }

        [Parameter] public bool ShowButtons { get; set; } = false;

        [Parameter, EditorRequired] public string Value { get; set; } = string.Empty;

        #endregion Public Properties

        #region Protected Methods

        protected override void OnParametersSet()
        {
            if (!IsEditing)
            {
                displayValue = Value;
            }
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task CancelAsync()
        {
            IsEditing = false;

            await OnCancel.InvokeAsync();
        }

        private async Task ConfirmAsync()
        {
            IsEditing = false;

            var trimmed = string.IsNullOrWhiteSpace(currentValue)
                ? null
                : currentValue.Trim();
            var previous = string.IsNullOrWhiteSpace(displayValue)
                ? null
                : displayValue.Trim();
            if (trimmed == previous) return;

            displayValue = trimmed ?? string.Empty;
            await OnConfirm.InvokeAsync(trimmed);
        }

        private async Task HandleBlur()
        {
            if (!ShowButtons)
            {
                await ConfirmAsync();
            }
        }

        private async Task HandleKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !ShowButtons)
            {
                await ConfirmAsync();
            }
            else if (e.Key == "Escape")
            {
                await CancelAsync();
            }
        }

        private void StartEditing()
        {
            currentValue = displayValue;
            IsEditing = true;
        }

        #endregion Private Methods
    }
}