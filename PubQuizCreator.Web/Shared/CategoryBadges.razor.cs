using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Web.Shared
{
    public partial class CategoryBadges
    {
        #region Public Properties

        [Parameter] public IEnumerable<Count> CategoryStats { get; set; } = [];

        [Parameter] public string CssClass { get; set; } = "";

        #endregion Public Properties
    }
}