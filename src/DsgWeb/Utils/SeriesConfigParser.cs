using System.Text.Json;
using DSG.DataStreamGenerator.Configuration;

namespace DsgWeb.Utils;

/// <summary>
/// Parses polymorphic <see cref="SeriesConfig"/> JSON independent of property order.
/// (System.Text.Json's built-in polymorphism requires the discriminator to be the
/// first property, which cannot be expected from API clients.)
/// </summary>
public static class SeriesConfigParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static SeriesConfig Parse(JsonElement element)
    {
        string? discriminator = null;
        foreach (var prop in element.EnumerateObject())
        {
            if (!string.Equals(prop.Name, "seriesType", StringComparison.OrdinalIgnoreCase))
                continue;

            discriminator = prop.Value.GetString();
            break;
        }

        var target = discriminator?.ToUpperInvariant() switch
        {
            "AR" => typeof(ARSeriesConfig),
            "ARMA" => typeof(ARMASeriesConfig),
            "ARIMA" => typeof(ARIMASeriesConfig),
            "ME" => typeof(MESeriesConfig),
            "MEC" => typeof(MECSeriesConfig),
            "MEMC" => typeof(MEMCSeriesConfig),
            "XF" => typeof(XFSeriesConfig),
            "XG" => typeof(XGSeriesConfig),
            "IS" => typeof(IntSeqSeriesConfig),
            _ => throw new JsonException(
                $"Unknown or missing seriesType discriminator: '{discriminator}'. " +
                "Expected one of AR, ARMA, ARIMA, ME, MEC, MEMC, XF, XG, IS.")
        };

        return (SeriesConfig)JsonSerializer.Deserialize(element.GetRawText(), target, Options)!;
    }
}
