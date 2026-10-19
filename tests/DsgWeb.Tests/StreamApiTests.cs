using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DsgWeb.Tests.Infrastructure;
using MQTTnet;
using MQTTnet.Client;

namespace DsgWeb.Tests;

/// <summary>
/// End-to-end coverage of the public API surface plus the actual MQTT output.
/// Each test gets a fresh in-process API instance; the Mosquitto container is shared.
/// </summary>
[Collection("dsg")]
public class StreamApiTests(MosquittoFixture broker) : IDisposable
{
    private readonly DsgWebFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Status_WithoutConfiguration_IsStopped()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/stream/status");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var status = await response.Content.ReadFromJsonAsync<JsonElement>();
        status.GetProperty("status").GetString().ShouldBe("stopped");
    }

    [Fact]
    public async Task Stop_WhenNotRunning_ReturnsConflict()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/stream/stop", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Start_WithoutConfiguration_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        (await client.PostAsync("/api/stream/start", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostConfig_InvalidPayload_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/stream/config", new
        {
            generator = new { id = "invalid", type = "not-a-type" },
            series = Array.Empty<object>()
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Stream_StartPublishesMqttMessages_AndStopStopsThem()
    {
        using var client = _factory.CreateClient();

        var topic = $"test/{Guid.NewGuid():N}/s1";
        var config = TestConfiguration.Create(broker.Host, broker.Port, topic);

        (await client.PostAsJsonAsync("/api/stream/config", config)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Subscribe before starting so no early message is missed.
        using var mqttClient = new MqttFactory().CreateMqttClient();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var messageCount = 0;
        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            Interlocked.Increment(ref messageCount);
            received.TrySetResult(Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
            return Task.CompletedTask;
        };
        await mqttClient.ConnectAsync(new MqttClientOptionsBuilder()
            .WithTcpServer(broker.Host, broker.Port)
            .WithClientId($"test-{Guid.NewGuid():N}")
            .Build());
        await mqttClient.SubscribeAsync(topic);

        (await client.PostAsync("/api/stream/start", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // A well-formed message must arrive on the configured topic.
        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(20)));
        completed.ShouldBeSameAs(received.Task);
        using (var message = JsonDocument.Parse(await received.Task))
        {
            message.RootElement.GetProperty("id").GetString().ShouldBe("s1");
            message.RootElement.TryGetProperty("value", out _).ShouldBeTrue();
        }

        // Status reflects the running stream, and a second start is rejected.
        var status = await client.GetFromJsonAsync<JsonElement>("/api/stream/status");
        status.GetProperty("status").GetString().ShouldBe("running");
        (await client.PostAsync("/api/stream/start", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await client.PostAsync("/api/stream/stop", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await WaitForStatusAsync(client, "stopped", TimeSpan.FromSeconds(20));

        // No further messages after stopping.
        var countAtStop = Volatile.Read(ref messageCount);
        await Task.Delay(TimeSpan.FromSeconds(2));
        Volatile.Read(ref messageCount).ShouldBe(countAtStop);

        await mqttClient.DisconnectAsync();
    }

    [Fact]
    public async Task PostConfig_WhileRunning_TakesEffectOnlyAfterRestart()
    {
        using var client = _factory.CreateClient();

        var firstTopic = $"test/{Guid.NewGuid():N}/s1";
        var secondTopic = $"test/{Guid.NewGuid():N}/s1";
        var first = TestConfiguration.Create(broker.Host, broker.Port, firstTopic);
        (await client.PostAsJsonAsync("/api/stream/config", first)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Subscribe to both topics before starting.
        using var mqttClient = new MqttFactory().CreateMqttClient();
        var firstReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var topic = e.ApplicationMessage.Topic;
            if (topic == firstTopic) firstReceived.TrySetResult(true);
            else if (topic == secondTopic) secondReceived.TrySetResult(true);
            return Task.CompletedTask;
        };
        await mqttClient.ConnectAsync(new MqttClientOptionsBuilder()
            .WithTcpServer(broker.Host, broker.Port)
            .WithClientId($"test-{Guid.NewGuid():N}")
            .Build());
        await mqttClient.SubscribeAsync(firstTopic);
        await mqttClient.SubscribeAsync(secondTopic);

        (await client.PostAsync("/api/stream/start", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Task.WhenAny(firstReceived.Task, Task.Delay(TimeSpan.FromSeconds(20)))).ShouldBeSameAs(firstReceived.Task);

        // Post a new configuration while the stream is running.
        var second = TestConfiguration.Create(broker.Host, broker.Port, secondTopic);
        var update = await client.PostAsJsonAsync("/api/stream/config", second);
        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updateBody = await update.Content.ReadFromJsonAsync<JsonElement>();
        updateBody.GetProperty("pending").GetBoolean().ShouldBeTrue();

        var status = await client.GetFromJsonAsync<JsonElement>("/api/stream/status");
        status.GetProperty("hasPendingConfiguration").GetBoolean().ShouldBeTrue();

        // The stored configuration is the new one, but the running stream keeps publishing
        // to the old topic and does not touch the new one.
        var stored = await client.GetFromJsonAsync<JsonElement>("/api/stream/config");
        stored.GetProperty("series")[0].GetProperty("topic").GetString().ShouldBe(secondTopic);
        (await Task.WhenAny(secondReceived.Task, Task.Delay(TimeSpan.FromSeconds(3))))
            .ShouldNotBeSameAs(secondReceived.Task);

        // Restart picks up the new configuration: messages now arrive on the new topic.
        (await client.PostAsync("/api/stream/stop", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await WaitForStatusAsync(client, "stopped", TimeSpan.FromSeconds(20));
        (await client.PostAsync("/api/stream/start", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var afterRestart = await client.GetFromJsonAsync<JsonElement>("/api/stream/status");
        afterRestart.GetProperty("status").GetString().ShouldBe("running");
        afterRestart.GetProperty("hasPendingConfiguration").GetBoolean().ShouldBeFalse();
        (await Task.WhenAny(secondReceived.Task, Task.Delay(TimeSpan.FromSeconds(20))))
            .ShouldBeSameAs(secondReceived.Task);

        (await client.PostAsync("/api/stream/stop", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await WaitForStatusAsync(client, "stopped", TimeSpan.FromSeconds(20));
        await mqttClient.DisconnectAsync();
    }

    private static async Task WaitForStatusAsync(HttpClient client, string expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var status = await client.GetFromJsonAsync<JsonElement>("/api/stream/status");
            if (status.GetProperty("status").GetString() == expected) return;
            await Task.Delay(200);
        }
        throw new TimeoutException($"Stream did not reach status '{expected}' within {timeout}.");
    }
}
