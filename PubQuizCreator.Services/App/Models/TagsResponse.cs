using System.Text.Json.Serialization;

namespace PubQuizCreator.Services.App.Models
{
    internal record TagsResponse(
        [property: JsonPropertyName("models")] List<OllamaModel>? Models);
}