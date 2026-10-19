namespace DsgWeb.Tests;

internal static class TestConfiguration
{
    public static object Create(string brokerHost, int brokerPort, string topic) => new
    {
        generator = new
        {
            id = "test-plant",
            description = "e2e test",
            environment = "DEVELOPMENT",
            seed = 42,
            interval = 100,
            dateTimeFormat = "yyyy-MM-dd-HH-mm-ss-fff",
            startDateTime = "2026-01-01T00:00:00",
            decimalPrecision = 4,
            duration = 600_000,
            type = "stream1",
            shuffle = false,
            disclosure = false,
            brokerHostName = brokerHost,
            brokerHostPort = brokerPort
        },
        series = new[]
        {
            new
            {
                seriesType = "ME",
                id = "s1",
                export = true,
                delay = 5,
                rank = 1,
                title = "s1",
                topic,
                interval = 200,
                outlierRatio1s = 0.0,
                outlierRatio2s = 0.0,
                initialValue = 0.0,
                c = 0.0,
                mean = 0.0,
                stdDev = 0.1,
                arguments = new[] { "i" },
                expression = "sin(i/5.0)"
            }
        }
    };
}
