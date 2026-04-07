using Microsoft.Extensions.Configuration;

namespace PubQuizCreator.Services
{
    public class SettingsService(IConfiguration configuration)
    {
        #region Private Fields

        private readonly string mediaPath = GetFolder(
            configValue: configuration["Media:StoragePath"],
            configKey: "Media:StoragePath");

        private readonly string templatesPath = GetFolder(
            configValue: configuration["Export:TemplatesPath"],
            configKey: "Export:TemplatesPath");

        #endregion Private Fields

        #region Public Properties

        public string MediaPath => mediaPath;

        #endregion Public Properties

        #region Public Methods

        public string GetTemplatePath(string name)
        {
            var fileName = configuration[$"Export:Templates:{name}"]
                ?? throw new InvalidOperationException(
                    $"Template '{name}' not configured under Export:Templates:{name}.");

            var result = Path.Combine(templatesPath, fileName);

            if (!File.Exists(result))
            {
                throw new FileNotFoundException(
                    $"Template file not found: {result}", result);
            }

            return result;
        }

        #endregion Public Methods

        #region Private Methods

        private static string GetFolder(string? configValue, string configKey)
        {
            if (string.IsNullOrWhiteSpace(configValue))
                throw new InvalidOperationException(
                    $"Configuration key '{configKey}' is required but not set.");

            if (!Path.IsPathRooted(configValue))
                throw new InvalidOperationException(
                    $"Configuration key '{configKey}' must be an absolute path. Current value: '{configValue}'");

            Directory.CreateDirectory(configValue);

            return configValue;
        }

        #endregion Private Methods
    }
}