using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared.Questions
{
    public partial class CategoryBadge
    {
        #region Public Properties

        [Parameter] public string ColorHex { get; set; } = "#aaaaaa";

        [Parameter] public bool IsUnusable { get; set; }

        [Parameter] public string Name { get; set; } = string.Empty;

        #endregion Public Properties
    }
}