using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PubQuizCreator.Core.Types;

namespace PubQuizCreator.Web.Shared.Medias
{
    public partial class MediaUpload
    {
        #region Public Properties

        [Parameter] public string? MediaFile { get; set; }

        [Parameter] public MediaType MediaType { get; set; } = MediaType.None;

        [Parameter] public EventCallback<(IBrowserFile File, MediaType Type)> OnFileChanged { get; set; }

        [Parameter] public EventCallback OnRemove { get; set; }

        [Parameter] public IBrowserFile? PendingFile { get; set; }

        #endregion Public Properties

        #region Private Methods

        private static MediaType DetectMediaType(IBrowserFile file)
        {
            // Prefer MIME type — more reliable than extension
            if (file.ContentType.StartsWith("image/")) return MediaType.Image;
            if (file.ContentType.StartsWith("audio/")) return MediaType.Audio;
            if (file.ContentType.StartsWith("video/")) return MediaType.Video;

            // Fallback to extension if MIME is missing/generic
            return Path.GetExtension(file.Name).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => MediaType.Image,
                ".mp3" or ".wav" or ".ogg" or ".m4a" => MediaType.Audio,
                ".mp4" or ".webm" or ".mov" => MediaType.Video,
                _ => MediaType.None
            };
        }

        private async Task OnFileSelected(InputFileChangeEventArgs e)
        {
            var file = e.File;
            var type = DetectMediaType(file);

            await OnFileChanged.InvokeAsync((file, type));
        }

        #endregion Private Methods
    }
}