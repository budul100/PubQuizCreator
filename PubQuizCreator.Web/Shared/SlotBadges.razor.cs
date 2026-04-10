using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Web.Shared
{
    public partial class SlotBadges
    {
        #region Public Properties

        [Parameter] public IEnumerable<SlotCategoryStat> CategoryStats { get; set; } = [];

        [Parameter] public string CssClass { get; set; } = "";

        #endregion Public Properties

        public record SlotCategoryStat(Category? Category, int Open, int Total);
    }
}