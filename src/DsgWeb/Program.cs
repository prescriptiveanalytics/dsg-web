using System.Text.Json.Serialization.Metadata;
using DsgWeb.Endpoints;
using DsgWeb.Services;
using DsgWeb.Utils;
using org.mariuszgromada.math.mxparser;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<DataStreamService>();
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNameCaseInsensitive = true;
    opts.SerializerOptions.AllowOutOfOrderMetadataProperties = true;
    opts.SerializerOptions.TypeInfoResolver = (opts.SerializerOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
        .WithAddedModifier(SeriesConfigJson.RequireDiscriminator);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapStreamEndpoints();

app.Run();
