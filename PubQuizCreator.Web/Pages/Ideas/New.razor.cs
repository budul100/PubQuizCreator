using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using DocumentFormat.OpenXml.Office.SpreadSheetML.Y2023.MsForms;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Helpers;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using PubQuizCreator.Services.Content;

namespace PubQuizCreator.Web.Pages.Ideas
{
    public partial class New
    {
        #region Private Fields

        private List<Category> categories = [];
        private Guid categoryId;
        private bool isTimeSensitive;
        private string? mediaFile;
        private MediaType mediaType = MediaType.None;
        private IBrowserFile? pendingFile;
        private bool saved;
        private bool saving;
        private string text = "";

        #endregion Private Fields

        #region Private Properties

        [Inject] private MediaService MediaService { get; set; } = null!;

        #endregion Private Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            AppState.SetPageTitle("Capture Idea");
            categories = (await CategoryService.GetAllAsync()).Where(c => !c.IsHidden).ToList();
        }

        #endregion Protected Methods

        #region Private Methods

        private void OnFileChanged((IBrowserFile File, MediaType Type) args)
        {
            pendingFile = args.File;
            mediaType = args.Type;
        }

        private void RemoveMediaAsync()
        {
            if (!string.IsNullOrEmpty(mediaFile))
            {
                MediaService.Delete(mediaFile);
            }

            mediaFile = null;
            mediaType = MediaType.None;
            pendingFile = null;
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            saving = true;
            saved = false;

            string? mediaFile = null;
            var mediaType = MediaType.None;

            if (pendingFile != null)
            {
                await using var stream = pendingFile.OpenReadStream(
                    maxAllowedSize: Constants.MaxUploadSizeBytes);

                mediaFile = await MediaService.SaveAsync(
                    stream: stream,
                    fileName: pendingFile.Name);

                mediaType = this.mediaType;
            }

            await IdeaService.CreateAsync(
                text: text.Trim(),
                categoryId: categoryId.NullIfEmpty(),
                isTimeSensitive: isTimeSensitive,
                mediaFile: mediaFile,
                mediaType: mediaType);

            text = "";
            categoryId = Guid.Empty;
            isTimeSensitive = false;
            pendingFile = null;

            this.mediaFile = null;
            this.mediaType = MediaType.None;

            saving = false;
            saved = true;
        }

        #endregion Private Methods
    }
}