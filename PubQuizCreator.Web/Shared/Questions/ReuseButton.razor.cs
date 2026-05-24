using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared.Questions
{
    public partial class ReuseButton
    {
        #region Public Properties

        [Parameter] public bool AllowReuse { get; set; }

        [Parameter] public bool IsUnusable { get; set; }

        [Parameter] public bool IsUsed { get; set; }
        
        [Parameter] public EventCallback<bool> OnToggle { get; set; }

        #endregion Public Properties
    }
}