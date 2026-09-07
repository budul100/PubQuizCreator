using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Web.Shared.Categories
{
    public partial class TalliesRow
    {
        #region Public Properties

        [Parameter] public string? HrefPrefix { get; set; }

        [Parameter] public IEnumerable<Tally> Tallies { get; set; } = [];

        #endregion Public Properties
    }
}