using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared
{
    public partial class PageHeader
    {
        #region Public Properties

        [Parameter] public RenderFragment? ChildContent { get; set; }

        [Parameter] public string Title { get; set; } = "";

        [Parameter] public RenderFragment? TitleSuffix { get; set; }

        #endregion Public Properties
    }
}