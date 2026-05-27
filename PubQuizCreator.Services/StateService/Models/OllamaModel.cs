using System.Text.Json.Serialization;

namespace PubQuizCreator.Services.StateService.Models
{
    internal record OllamaModel(
        [property: JsonPropertyName("name")] string Name);
}