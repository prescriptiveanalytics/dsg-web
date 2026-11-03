using DsgWeb.Services;

namespace DsgWeb.Models;

/// <summary>
/// Error body returned with 4xx responses.
/// </summary>
public record ErrorResponse(string? Error);

/// <summary>
/// Response for <c>POST /api/stream/config</c>.
/// <see cref="Pending"/> is <c>true</c> if the stream is running and the configuration takes effect on restart.
/// </summary>
public record ConfigStoredResponse(string Message, bool Pending);

/// <summary>
/// Response for <c>POST /api/stream/start</c>.
/// </summary>
public record StartStreamResponse(string Message, StreamStatusResponse Status);

/// <summary>
/// Response for <c>POST /api/stream/stop</c>.
/// </summary>
public record StopStreamResponse(string Message);
