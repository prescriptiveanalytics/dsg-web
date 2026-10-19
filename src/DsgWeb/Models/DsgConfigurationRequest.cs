using System.Text.Json;
using DSG.DataStreamGenerator.Configuration;

namespace DsgWeb.Models;

/// <summary>
/// POST body for <c>/api/stream/config</c>.
/// Each entry in <see cref="Series"/> must carry a <c>seriesType</c> discriminator
/// (AR, ARMA, ARIMA, ME, MEC, MEMC, XF, XG, IS) in any position.
/// </summary>
public class DsgConfigurationRequest
{
    public GeneratorConfig Generator { get; set; } = new();
    public List<JsonElement> Series { get; set; } = [];
}
