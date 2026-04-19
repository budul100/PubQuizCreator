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

        #endregion Private Methods
    }
}