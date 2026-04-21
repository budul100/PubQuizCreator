using Microsoft.AspNetCore.Components.Forms;
using PubQuizCreator.Core;
using PubQuizCreator.Services;

namespace PubQuizCreator.Web.Pages.Settings
{
    public partial class Index
    {
        #region Private Fields

        private DateTime? savedAt;
        private string? saveError;
        private bool saving;
        private Core.Models.Settings? settings;

        #endregion Private Fields

        #region Private Enums

        private enum TemplateRole
        {
            Questions,
            Answers
        }

        #endregion Private Enums

        #region Protected Methods

        protected override void OnInitialized()
        {
            AppState.SetPageTitle("Settings");
            settings = SettingsService.Read();
        }

        #endregion Protected Methods

        #region Private Methods

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

        private async Task UploadAdditionalAsync(InputFileChangeEventArgs e)
        {
            if (settings == null) return;
            try
            {
                var file = e.File;
                var fileName = Path.GetFileName(file.Name);

                await using var stream = file.OpenReadStream(
                    maxAllowedSize: Constants.MaxUploadSizeBytes);
                await SettingsService.SaveFileAsync(
                    content: stream,
                    fileName: fileName);

                var existingIndex = settings.AdditionalFiles.FindIndex(f => string.Equals(
                    a: f,
                    b: fileName,
                    comparisonType: StringComparison.OrdinalIgnoreCase));

                if (existingIndex >= 0)
                {
                    settings.AdditionalFiles[existingIndex] = fileName;
                }
                else
                {
                    settings.AdditionalFiles.Add(fileName);
                }

                await SettingsService.SaveAsync(settings);

                savedAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                saveError = $"Upload failed: {ex.Message}";
            }
        }

        private async Task UploadTemplateAsync(InputFileChangeEventArgs e, TemplateRole role)
        {
            if (settings == null) return;
            try
            {
                var file = e.File;
                var fileName = Path.GetFileName(file.Name);

                await using var stream = file.OpenReadStream(
                    maxAllowedSize: Constants.MaxUploadSizeBytes);
                await SettingsService.SaveFileAsync(
                    content: stream,
                    fileName: fileName);

                if (role == TemplateRole.Questions)
                {
                    settings.TemplateQuestions = fileName;
                }
                else
                {
                    settings.TemplateAnswers = fileName;
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