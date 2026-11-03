using DSG.DataStreamGenerator;
using DSG.DataStreamGenerator.Configuration;
using DsgWeb.Models;
using JetBrains.Annotations;

namespace DsgWeb.Services;

[UsedImplicitly]
public record StreamStatusResponse(
    string Status,
    bool HasConfiguration,
    bool HasPendingConfiguration,
    DateTime? StartedAt,
    string? GeneratorId,
    string? GeneratorType);

public record UpdateConfigResult([UsedImplicitly] bool AppliedOnNextStart, bool WasRunning);

public sealed class DataStreamService(ILogger<DataStreamService> logger)
{
    private readonly Lock _lock = new();

    private GeneratorConfig? _pendingGenerator;
    private List<SeriesConfig>? _pendingSeries;
    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private DateTime? _startedAt;
    private GeneratorConfig? _activeGenerator;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _runTask is { IsCompleted: false };
            }
        }
    }

    public UpdateConfigResult UpdateConfiguration(DsgConfigurationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var series = request.Series ?? [];
        Validate(request.Generator, series);
        request.Generator.EnsureInitialized();

        lock (_lock)
        {
            bool wasRunning = _runTask is { IsCompleted: false };
            _pendingGenerator = request.Generator;
            _pendingSeries = series;
            return new UpdateConfigResult(AppliedOnNextStart: true, WasRunning: wasRunning);
        }
    }

    public DsgConfigurationResponse? GetConfiguration()
    {
        lock (_lock)
        {
            if (_pendingGenerator is null || _pendingSeries is null)
                return null;
            return new DsgConfigurationResponse
            {
                Generator = _pendingGenerator,
                Series = _pendingSeries
            };
        }
    }

    public StreamStatusResponse GetStatus()
    {
        lock (_lock)
        {
            bool running = _runTask is { IsCompleted: false };
            return new StreamStatusResponse(
                Status: running ? "running" : "stopped",
                HasConfiguration: _pendingGenerator is not null,
                HasPendingConfiguration: running && _pendingGenerator is not null && !ReferenceEquals(_pendingGenerator, _activeGenerator),
                StartedAt: running ? _startedAt : null,
                GeneratorId: running ? _activeGenerator?.Id : _pendingGenerator?.Id,
                GeneratorType: running ? _activeGenerator?.Type : _pendingGenerator?.Type);
        }
    }

    public bool TryStart(out string? error)
    {
        lock (_lock)
        {
            if (_runTask is { IsCompleted: false })
            {
                error = "Data stream is already running.";
                return false;
            }

            if (_pendingGenerator is null || _pendingSeries is null)
            {
                error = "No configuration has been posted yet.";
                return false;
            }

            GeneratorConfig generator;
            IReadOnlyDictionary<string, SeriesConfig> seriesDict;
            try
            {
                generator = _pendingGenerator;
                seriesDict = _pendingSeries.ToDictionary(s => s.Id, s => s);
                generator.EnsureInitialized();
                // Early validation via runner constructor (checks type + non-empty series).
                _ = new DataStreamRunner(generator, seriesDict);
            }
            catch (Exception ex)
            {
                error = $"Invalid configuration: {ex.Message}";
                return false;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            var runner = new DataStreamRunner(generator, seriesDict);
            var token = cts.Token;

            logger.LogInformation("Starting data stream (Id={Id}, Type={Type})", generator.Id, generator.Type);
            var runTask = Task.Run(() => runner.Run(token), token);

            _cts = cts;
            _runTask = runTask;
            _startedAt = DateTime.UtcNow;
            _activeGenerator = generator;

            runTask.ContinueWith(t =>
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_runTask, t))
                    {
                        _runTask = null;
                        _cts?.Dispose();
                        _cts = null;
                        _startedAt = null;
                        _activeGenerator = null;
                    }
                }

                if (t.IsFaulted)
                    logger.LogError(t.Exception, "Data stream faulted");
                else if (t.IsCanceled)
                    logger.LogInformation("Data stream was cancelled");
                else
                    logger.LogInformation("Data stream completed");
            }, TaskScheduler.Default);

            error = null;
            return true;
        }
    }

    public bool TryStop(out string? error)
    {
        lock (_lock)
        {
            if (_runTask is not { IsCompleted: false })
            {
                error = "Data stream is not running.";
                return false;
            }

            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed by completion continuation; treat as stopped.
            }

            logger.LogInformation("Stop requested for data stream");
            error = null;
            return true;
        }
    }

    private static void Validate(GeneratorConfig generator, List<SeriesConfig> series)
    {
        if (generator is null)
            throw new ArgumentException("Field 'generator' is required.");
        if (series.Count == 0)
            throw new ArgumentException("Field 'series' must contain at least one series configuration.");
        if (string.IsNullOrWhiteSpace(generator.Type))
            throw new ArgumentException("Generator configuration must provide a type (stream1, stream2, generate1, generate2).");

        var type = generator.Type.Trim();
        var allowed = new[]
        {
            DataStreamRunner.TYPE_STREAM_DATETIMEBASED_SINGLETHREADED,
            DataStreamRunner.TYPE_STREAM_DATETIMEBASED_MULTITASKED,
            DataStreamRunner.TYPE_GENERATE_DATETIMEBASED,
            DataStreamRunner.TYPE_GENERATE_EVENTCOUNTBASED
        };
        if (!allowed.Contains(type))
            throw new ArgumentException($"Unknown generator type '{type}'. Expected one of: {string.Join(", ", allowed)}.");

        var ids = new HashSet<string>();
        foreach (var s in series)
        {
            if (string.IsNullOrWhiteSpace(s.Id))
                throw new ArgumentException("Every series configuration must provide an Id.");
            if (!ids.Add(s.Id))
                throw new ArgumentException($"Duplicate series Id '{s.Id}'.");
        }
    }
}
