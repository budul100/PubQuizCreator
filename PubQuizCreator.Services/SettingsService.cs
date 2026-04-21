using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Services
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
                var defaultPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "settings.override.json");

                return defaultPath;
            }

            if (!Path.IsPathRooted(result))
            {
                result = Path.Combine(
                    AppContext.BaseDirectory,
                    result);
            }

            return result;
        }

        public string GetFormatTitle() => configuration["Export:TitleFormat"]
            ?? "Question {position}";

        public IEnumerable<string> GetPathAdditionals()
        {
            var additionalFiles = GetAdditionalFiles().ToArray();

            var templatesPath = GetPathTemplates() ?? "";

            foreach (var additionalFile in additionalFiles)
            {
                var path = Path.Combine(
                    templatesPath,
                    additionalFile);

                if (File.Exists(path))
                {
                    yield return path;
                }
            }
        }

        public string GetPathMedia() => GetFolder(
            configValue: configuration["Media:StoragePath"],
            configKey: "Media:StoragePath");

        public string GetPathTemplate(string name)
        {
            var fileName = configuration[$"Export:Templates:{name}"]
                ?? throw new InvalidOperationException(
                    $"Template '{name}' not configured under Export:Templates:{name}.");

            var result = Path.Combine(
                GetPathTemplates(),
                fileName);

            if (!File.Exists(result))
            {
                throw new FileNotFoundException(
                    $"Template file not found: {result}", result);
            }

            return result;
        }

        public string GetPathTemplates() => GetFolder(
            configValue: configuration["Export:TemplatesPath"],
            configKey: "Export:TemplatesPath");

        public Settings Read()
        {
            return new()
            {
                AdditionalFiles = GetAdditionalFiles()?.ToList() ?? [],
                AiPrompt = configuration["Quiz:PromptTemplate"] ?? "",
                AiUrl = configuration["Quiz:AiUrl"] ?? "",
                PrintFontSizeDefault = configuration.GetValue("Print:FontSizeDefault", 8f),
                PrintFontSizeHeader = configuration.GetValue("Print:FontSizeHeader", 11f),
                TemplateAnswers = configuration["Export:Templates:Answers"] ?? "",
                TemplateQuestions = configuration["Export:Templates:Questions"] ?? "",
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
                    Templates = new
                    {
                        Questions = settings.TemplateQuestions,
                        Answers = settings.TemplateAnswers,
                    },
                    settings.AdditionalFiles,
                },
                Quiz = new
                {
                    PromptTemplate = settings.AiPrompt,
                    settings.AiUrl,
                    settings.TextShortWarnLength,
                },
                Print = new
                {
                    FontSizeDefault = settings.PrintFontSizeDefault,
                    FontSizeHeader = settings.PrintFontSizeHeader,
                },
            };

            var json = JsonSerializer.Serialize(
                value: data,
                options: jsonOptions);

            await File.WriteAllTextAsync(
                path: overridePath,
                contents: json,
                cancellationToken: ct);
        }

        public async Task SaveFileAsync(Stream content, string fileName, CancellationToken ct = default)
        {
            var safeName = Path.GetFileName(fileName);

            if (string.IsNullOrWhiteSpace(safeName))
            {
                throw new ArgumentException("Invalid filename.");
            }

            var targetPath = Path.Combine(
                GetPathTemplates(),
                safeName);

            await using var fs = new FileStream(
                path: targetPath,
                mode: FileMode.Create,
                access: FileAccess.Write);

            await content.CopyToAsync(
                destination: fs,
                cancellationToken: ct);
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

        private List<string> GetAdditionalFiles()
        {
            return configuration
                .GetSection("Export:AdditionalFiles")
                .Get<List<string>>() ?? [];
        }

        #endregion Private Methods
    }
}