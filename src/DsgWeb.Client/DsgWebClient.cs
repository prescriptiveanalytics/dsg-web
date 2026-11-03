using System.Text.Json;
using System.Text.Json.Serialization;

namespace DsgWeb.Client;

/// <summary>
/// Client for the DsgWeb REST API.
/// Pass an <see cref="HttpClient"/> whose <see cref="HttpClient.BaseAddress"/> points to the DsgWeb instance.
/// </summary>
public partial class DsgWebClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        settings.PropertyNameCaseInsensitive = true;
        settings.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        settings.AllowOutOfOrderMetadataProperties = true;
    }
}
