using System.Text.Json.Serialization;

namespace PubQuizCreator.Services.StateService.Models
{
    internal record TagsResponse(
        [property: JsonPropertyName("models")] List<OllamaModel>? Models);
}