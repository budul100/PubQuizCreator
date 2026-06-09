using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Services.Content
{
    public class SettingsService(IConfiguration configuration)
    {
        #region Private Fields

        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly string overridePath = GetPathOverride(configuration);

        #endregion Private Fields

        #region Public Methods

        public static string GetPathOverride(IConfiguration configuration)
        {
            var result = configuration["App:SettingsOverridePath"];

            if (string.IsNullOrWhiteSpace(result))
            {
                return Path.Combine(AppContext.BaseDirectory, "settings.override.json");
            }

            if (!Path.IsPathRooted(result))
            {
                result = Path.Combine(AppContext.BaseDirectory, result);
            }

            return result;
        }

        public string GetFormatTitle() => configuration["Export:TitleFormat"]
            ?? "Question {position}";

        public string GetPathMedia() => GetFolder("Media:StoragePath");

        public string GetPathTemplates() => GetFolder("Export:TemplatesPath");

        public IEnumerable<string> GetPptxTemplateNames()
        {
            return configuration
                .GetSection("Export:PptxTemplates")
                .Get<List<string>>()
                ?.Where(f => !string.IsNullOrWhiteSpace(f))
                ?? [];
        }

        public string? GetPptxTemplatePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            var path = Path.Combine(GetPathTemplates(), fileName);

            return File.Exists(path) ? path : null;
        }

        public Settings Read()
        {
            return new()
            {
                AiPrompt = configuration["Quiz:PromptTemplate"] ?? "",
                AiUrl = configuration["Quiz:AiUrl"] ?? "",
                PrintFontSizeDefault = configuration.GetValue("Print:FontSizeDefault", 8f),
                PrintFontSizeHeader = configuration.GetValue("Print:FontSizeHeader", 11f),
                PptxTemplates = GetPptxTemplateNames().ToList(),
                TextShortWarnLength = configuration.GetValue("Quiz:TextShortWarnLength", 100),
                TitleFormat = configuration["Export:TitleFormat"] ?? "Question {position}"
            };
        }

        public async Task SaveAsync(Settings settings, CancellationToken ct = default)
        {
            var data = new
            {
                Export = new
                {
                    settings.TitleFormat,
                    settings.PptxTemplates
                },

                Quiz = new
                {
                    PromptTemplate = settings.AiPrompt,
                    settings.AiUrl,
                    settings.TextShortWarnLength
                },

                Print = new
                {
                    FontSizeDefault = settings.PrintFontSizeDefault,
                    FontSizeHeader = settings.PrintFontSizeHeader
                }
            };

            var json = JsonSerializer.Serialize(data, jsonOptions);
            await File.WriteAllTextAsync(overridePath, json, ct);

            // Reload configuration so in-memory values reflect the saved state
            if (configuration is IConfigurationRoot root)
            {
                root.Reload();
            }
        }

        public async Task SaveFileAsync(Stream content, string fileName,
            CancellationToken ct = default)
        {
            var dir = GetPathTemplates();
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, fileName);

            await using var fs = File.Create(path);
            await content.CopyToAsync(fs, ct);
        }

        #endregion Public Methods

        #region Private Methods

        private string GetFolder(string configKey)
        {
            var configValue = configuration[configKey];

            if (!string.IsNullOrWhiteSpace(configValue))
            {
                return Path.IsPathRooted(configValue)
                    ? configValue
                    : Path.Combine(AppContext.BaseDirectory, configValue);
            }

            return Path.Combine(
                AppContext.BaseDirectory,
                "export");
        }

        #endregion Private Methods
    }
}