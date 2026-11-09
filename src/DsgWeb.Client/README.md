# DsgWeb.Client

Typed HTTP client for the [DsgWeb](https://github.com/prescriptiveanalytics/dsg-web) REST API.
The configuration types (`GeneratorConfig`, `SeriesConfig` and its subtypes) come from the
[DataStreamGenerator](https://www.nuget.org/packages/DataStreamGenerator) package.

```csharp
using DSG.DataStreamGenerator.Configuration;
using DsgWeb.Client;

var client = new DsgWebClient(new HttpClient { BaseAddress = new Uri("http://localhost:8080") });

await client.PostConfigAsync(new DsgConfigurationRequest
{
    Generator = new GeneratorConfig
    {
        Id = "plantX",
        Environment = "PRODUCTION",
        Type = "stream1",
        Interval = 100,
        BrokerHostName = "mosquitto",
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

await client.StartStreamAsync();
var status = await client.GetStatusAsync();
```

Failed requests throw an `ApiException<ErrorResponse>` that carries the status code and error message.
