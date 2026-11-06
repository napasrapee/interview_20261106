using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", async (IDatabaseHealthCheck database, CancellationToken cancellationToken) =>
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(3));

    var healthy = await database.IsHealthyAsync(timeout.Token);
    return Results.Json(
        new
        {
            status = healthy ? "healthy" : "unhealthy",
            database = healthy ? "up" : "down",
            timestamp = DateTimeOffset.UtcNow,
        },
        statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.Run();
