using DsgWeb.Models;
using DsgWeb.Services;

namespace DsgWeb.Endpoints;

public static class StreamEndpoints
{
    public static void MapStreamEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stream").WithTags("Stream");

        group.MapPost("/config", (DsgConfigurationRequest request, DataStreamService service) =>
        {
            try
            {
                var result = service.UpdateConfiguration(request);
                if (result.WasRunning)
                    return Results.Ok(new
                    {
                        message = "Configuration stored. It will take effect when the data stream is restarted.",
                        pending = true
                    });
                return Results.Ok(new
                {
                    message = "Configuration stored. It will take effect on the next start.",
                    pending = false
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("PostConfig")
        .WithSummary("POST a new DSG configuration. Takes effect on (re)start.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/config", (DataStreamService service) =>
        {
            var config = service.GetConfiguration();
            return config is null
                ? Results.NotFound(new { error = "No configuration has been posted yet." })
                : Results.Ok(config);
        })
        .WithName("GetConfig")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/start", (DataStreamService service) =>
        {
            if (service.IsRunning)
                return Results.Conflict(new { error = "Data stream is already running." });
            if (!service.TryStart(out var error))
            {
                // No config posted -> 400; anything else at this point is a conflict.
                var status = error?.Contains("No configuration") == true ? StatusCodes.Status400BadRequest : StatusCodes.Status409Conflict;
                return Results.Json(new { error }, statusCode: status);
            }
            return Results.Ok(new { message = "Data stream started.", status = service.GetStatus() });
        })
        .WithName("StartStream")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/stop", (DataStreamService service) =>
        {
            if (!service.TryStop(out _))
                return Results.Conflict(new { error = "Data stream is not running." });
            return Results.Ok(new { message = "Stop requested for data stream." });
        })
        .WithName("StopStream")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/status", (DataStreamService service) => Results.Ok(service.GetStatus()))
        .WithName("GetStatus")
        .Produces(StatusCodes.Status200OK);
    }
}
