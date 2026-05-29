using System.Text.Json.Serialization;

namespace PubQuizCreator.Services.App.Models
{
    internal record OllamaModel(
        [property: JsonPropertyName("name")] string Name);
}