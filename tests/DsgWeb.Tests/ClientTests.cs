using DSG.DataStreamGenerator.Configuration;
using DsgWeb.Client;
using DsgWeb.Tests.Infrastructure;

namespace DsgWeb.Tests;

/// <summary>
/// Checks that the generated <see cref="DsgWebClient"/> works against the API, including the DSG configuration types.
/// </summary>
public class ClientTests : IDisposable
{
    private readonly DsgWebFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private DsgWebClient CreateClient() => new(_factory.CreateClient());

    [Fact]
    public async Task GetStatus_WithoutConfiguration_IsStopped()
    {
        var status = await CreateClient().GetStatusAsync();

        status.Status.ShouldBe("stopped");
        status.HasConfiguration.ShouldBeFalse();
    }

    [Fact]
    public async Task StartStream_WithoutConfiguration_ThrowsBadRequest()
    {
        var ex = await Should.ThrowAsync<ApiException<ErrorResponse>>(() => CreateClient().StartStreamAsync());

        ex.StatusCode.ShouldBe(400);
        ex.Result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostConfig_RoundTripsSeriesTypes()
    {
        var client = CreateClient();

        var stored = await client.PostConfigAsync(new DsgConfigurationRequest
        {
            Generator = new GeneratorConfig
            {
                Id = "plantX",
                Environment = "PRODUCTION",
                Type = "stream1",
                Interval = 100,
                Duration = 1000,
                BrokerHostName = "localhost",
                BrokerHostPort = 1883
            },
            Series =
            [
                new MESeriesConfig
                {
                    Id = "s1",
                    Topic = "raw/plantx/s1",
                    Interval = 200,
                    Arguments = ["i"],
                    Expression = "sin(i/5.0)"
                }
            ]
        });
        stored.Pending.ShouldBeFalse();

        var config = await client.GetConfigAsync();

        config.Generator!.Id.ShouldBe("plantX");
        var series = config.Series!.ShouldHaveSingleItem().ShouldBeOfType<MESeriesConfig>();
        series.Id.ShouldBe("s1");
        series.Expression.ShouldBe("sin(i/5.0)");
    }
}
