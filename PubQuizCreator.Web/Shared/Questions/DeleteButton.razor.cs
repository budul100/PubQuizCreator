using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared.Questions
{
    public partial class DeleteButton
    {
        #region Public Properties

        [Parameter] public bool IsUsed { get; set; }

        [Parameter] public EventCallback OnDelete { get; set; }

        #endregion Public Properties
    }
}