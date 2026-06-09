using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared.Categories
{
    public partial class SlotBadge
    {
        #region Public Properties

        [Parameter] public string CssClass { get; set; } = null;

        [Parameter] public int Open { get; set; }

        [Parameter] public int Total { get; set; }

        #endregion Public Properties
    }
}