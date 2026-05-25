using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using PubQuizCreator.Core;
using PubQuizCreator.Services;

namespace PubQuizCreator.Web.Pages.Settings
{
    public partial class Index
    {
        #region Private Fields

        private bool autoOpenCapture;
        private DateTime? savedAt;
        private string? saveError;
        private bool saving;
        private Core.Models.Settings? settings;

        #endregion Private Fields

        #region Protected Methods

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender) return;
            autoOpenCapture = await JS.InvokeAsync<bool>(Constants.AutoOpenGetter);
            StateHasChanged();
        }

        protected override void OnInitialized()
        {
            AppState.SetPageTitle("Settings");
            settings = SettingsService.Read();
        }

        #endregion Protected Methods

        #region Private Methods

        private void RemoveTemplate(string fileName)
        {
            if (settings == null) return;

            settings.PptxTemplates.Remove(fileName);
            // Note: does not delete the file from disk — only removes it from the list
        }

        private async Task SaveAsync()
        {
            if (settings == null) return;

            saving = true;
            saveError = null;
            savedAt = null;

            try
            {
                await SettingsService.SaveAsync(settings);
                savedAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                saveError = $"Error: {ex.Message}";
            }
            finally
            {
                saving = false;
            }
        }

        private async Task SaveAutoOpenCaptureAsync()
        {
            await JS.InvokeVoidAsync(Constants.AutoOpenSetter, autoOpenCapture);
        }

        private async Task UploadTemplateAsync(InputFileChangeEventArgs e)
        {
            if (settings == null) return;
            try
            {
                var file = e.File;
                var fileName = Path.GetFileName(file.Name);

                await using var stream = file.OpenReadStream(
                    maxAllowedSize: Constants.MaxUploadSizeBytes);
                await SettingsService.SaveFileAsync(content: stream, fileName: fileName);

                // Add to list if not already present (case-insensitive)
                if (!settings.PptxTemplates.Any(f =>
                    string.Equals(f, fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    settings.PptxTemplates.Add(fileName);
                }

                await SettingsService.SaveAsync(settings);
                savedAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                saveError = $"Upload failed: {ex.Message}";
            }
        }

        #endregion Private Methods
    }
}