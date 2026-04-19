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

        private readonly string overridePath = GetOverridePath(configuration);

        #endregion Private Fields

        #region Public Properties

        public string MediaPath => GetFolder(
            configValue: configuration["Media:StoragePath"],
            configKey: "Media:StoragePath");

        public string TemplatesPath => GetFolder(
            configValue: configuration["Export:TemplatesPath"],
            configKey: "Export:TemplatesPath");

        #endregion Public Properties

        #region Public Methods

        public static string GetOverridePath(IConfiguration configuration)
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

        public string GetTemplatePath(string name)
        {
            var fileName = configuration[$"Export:Templates:{name}"]
                ?? throw new InvalidOperationException(
                    $"Template '{name}' not configured under Export:Templates:{name}.");

            var result = Path.Combine(
                TemplatesPath,
                fileName);

            if (!File.Exists(result))
            {
                throw new FileNotFoundException(
                    $"Template file not found: {result}", result);
            }

            return result;
        }

        public Settings Read() => new()
        {
            OllamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "",
            OllamaEmbeddingModel = configuration["Ollama:EmbeddingModel"] ?? "",
            TemplateQuestions = configuration["Export:Templates:Questions"] ?? "",
            TemplateAnswers = configuration["Export:Templates:Answers"] ?? "",
            AiPrompt = configuration["Quiz:PromptTemplate"] ?? "",
            AiUrl = configuration["Quiz:AiUrl"] ?? "",
            TextShortWarnLength = configuration.GetValue("Quiz:TextShortWarnLength", 100),
            PrintFontSizeDefault = configuration.GetValue("Print:FontSizeDefault", 8f),
            PrintFontSizeHeader = configuration.GetValue("Print:FontSizeHeader", 11f),
        };

        public async Task SaveAsync(Settings model, CancellationToken ct = default)
        {
            var data = new
            {
                Ollama = new
                {
                    BaseUrl = model.OllamaBaseUrl,
                    EmbeddingModel = model.OllamaEmbeddingModel,
                },
                Export = new
                {
                    Templates = new
                    {
                        Questions = model.TemplateQuestions,
                        Answers = model.TemplateAnswers,
                    },
                },
                Quiz = new
                {
                    PromptTemplate = model.AiPrompt,
                    AiUrl = model.AiUrl,
                    TextShortWarnLength = model.TextShortWarnLength,
                },
                Print = new
                {
                    FontSizeDefault = model.PrintFontSizeDefault,
                    FontSizeHeader = model.PrintFontSizeHeader,
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