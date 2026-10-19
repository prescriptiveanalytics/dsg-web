using Testcontainers.Mosquitto;

namespace DsgWeb.Tests.Infrastructure;

public sealed class MosquittoFixture : IAsyncLifetime
{
    private readonly MosquittoContainer _container = new MosquittoBuilder("eclipse-mosquitto:2.1-alpine")
        .Build();

    public string Host { get; private set; } = "";
    public int Port { get; private set; }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Host = _container.Hostname;
        Port = _container.GetMappedPublicPort(1883);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
