using Microsoft.AspNetCore.Components;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Web.Shared
{
    public partial class CategorySelector
    {
        #region Public Properties

        [Parameter] public List<Category> Categories { get; set; } = [];

        [Parameter] public string ConfirmLabel { get; set; } = "Add";

        [Parameter] public EventCallback OnCancel { get; set; }

        [Parameter] public EventCallback OnConfirm { get; set; }

        [Parameter] public Guid? SelectedCategoryId { get; set; }

        [Parameter] public EventCallback<Guid?> SelectedCategoryIdChanged { get; set; }

        #endregion Public Properties
    }
}