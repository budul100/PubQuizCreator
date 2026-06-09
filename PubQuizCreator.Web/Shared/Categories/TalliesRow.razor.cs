using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Web.Shared.Categories
{
    public partial class TalliesRow
    {
        [Parameter] public IEnumerable<Tally> Tallies { get; set; } = [];
    }
}