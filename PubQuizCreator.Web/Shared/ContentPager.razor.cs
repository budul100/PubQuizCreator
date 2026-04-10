using Microsoft.AspNetCore.Components;

namespace PubQuizCreator.Web.Shared
{
    public partial class ContentPager
    {
        #region Public Properties

        [Parameter, EditorRequired] public int CurrentPage { get; set; }

        [Parameter, EditorRequired] public EventCallback<int> PageChanged { get; set; }

        [Parameter, EditorRequired] public int PageSize { get; set; }

        [Parameter, EditorRequired] public int TotalItems { get; set; }

        #endregion Public Properties

        #region Private Properties

        private int EndPage => Math.Min(TotalPages, StartPage + 4);

        // Show max 5 page buttons around current page
        private int StartPage => Math.Max(1, CurrentPage - 2);

        private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize));

        #endregion Private Properties
    }
}