using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared
{
    public partial class LoadingSpinner
    {
        #region Public Properties

        [Parameter] public string Message { get; set; } = "Loading…";

        #endregion Public Properties
    }
}