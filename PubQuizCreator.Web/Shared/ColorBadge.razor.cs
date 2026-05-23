using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared
{
    public partial class ColorBadge

    {
        #region Public Properties

        [Parameter] public RenderFragment? ChildContent { get; set; }

        [Parameter] public string ColorHex { get; set; } = "#95a5a6";

        [Parameter] public string CssClass { get; set; } = "";

        [Parameter] public string? Href { get; set; }

        [Parameter] public string Name { get; set; } = "";

        [Parameter] public string Style { get; set; } = "";

        #endregion Public Properties
    }
}