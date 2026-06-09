using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Web.Shared.Categories
{
    public partial class TalliesList
    {
        #region Public Properties

        [Parameter] public IEnumerable<Tally> Tallies { get; set; } = [];

        [Parameter] public string CssClass { get; set; } = "";

        #endregion Public Properties
    }
}
