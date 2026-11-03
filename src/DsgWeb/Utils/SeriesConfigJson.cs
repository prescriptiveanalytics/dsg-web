using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DSG.DataStreamGenerator.Configuration;

namespace DsgWeb.Utils;

/// <summary>
/// JSON contract adjustments for the polymorphic <see cref="SeriesConfig"/>.
/// </summary>
public static class SeriesConfigJson
{
    /// <summary>
    /// <see cref="SeriesConfig"/> is not abstract, so System.Text.Json silently deserializes an entry without
    /// a <c>seriesType</c> discriminator as a plain <see cref="SeriesConfig"/>. Entries with a known discriminator
    /// are always created as their subtype, so creating the base type itself means the discriminator is missing.
    /// Throwing a <see cref="JsonException"/> makes ASP.NET Core answer with 400 Bad Request.
    /// </summary>
    public static void RequireDiscriminator(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(SeriesConfig))
            typeInfo.CreateObject = static () => throw new JsonException(
                "Missing seriesType discriminator. Expected one of AR, ARMA, ARIMA, ME, MEC, MEMC, XF, XG, IS.");
    }
}
