using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Types;

namespace PubQuizCreator.Web.Shared.Medias
{
    public partial class MediaIcon
    {
        #region Public Properties

        [Parameter] public string CssClass { get; set; } = "ms-1";

        [Parameter] public string Prefix { get; set; } = " | ";

        [Parameter] public MediaType? MediaType { get; set; }

        #endregion Public Properties
    }
}