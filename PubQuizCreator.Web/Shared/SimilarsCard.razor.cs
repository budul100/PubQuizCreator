using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Web.Shared
{
    public partial class SimilarsCard
    {
        #region Public Properties

        [Parameter] public bool HasSearched { get; set; }

        [Parameter] public bool IsSearching { get; set; }

        [Parameter] public List<QuestionSimilar> SimilarQuestions { get; set; } = [];

        #endregion Public Properties
    }
}