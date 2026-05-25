using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Types;

namespace PubQuizCreator.Web.Shared.Medias
{
    public partial class MediaLink
    {
        #region Public Properties

        [Parameter] public string? MediaFile { get; set; }

        [Parameter, EditorRequired] public MediaType MediaType { get; set; }

        [Parameter] public string Prefix { get; set; } = " | ";

        #endregion Public Properties
    }
}