using DSG.DataStreamGenerator.Configuration;

namespace DsgWeb.Models;

/// <summary>
/// Response for <c>GET /api/stream/config</c>: the active/pending configuration
/// with series materialized as their concrete DSG types.
/// </summary>
public class DsgConfigurationResponse
{
    public GeneratorConfig Generator { get; set; } = new();
    public List<SeriesConfig> Series { get; set; } = [];
}
