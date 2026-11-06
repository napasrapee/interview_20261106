using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using ProjectMonitoring.Api;
using ProjectMonitoring.Application;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Features;
using ProjectMonitoring.Infrastructure;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Secrets created by scripts/init-secrets.sh: one file per setting, named like Jwt__Secret.
    // Reading files keeps them out of the container environment and `docker inspect`.
    if (builder.Configuration["SECRETS_DIR"] is { Length: > 0 } secretsDirectory)
    {
        builder.Configuration.AddKeyPerFile(source =>
        {
            source.FileProvider = new PhysicalFileProvider(secretsDirectory);
            source.IgnoreCondition = name => !name.Contains("__");
        });
    }

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApiSecurity(builder.Configuration);
    builder.Services.AddControllers()
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new NullCharacterRejectingConverter()))
        .ConfigureApiBehaviorOptions(options =>
        {
            // Status-only results go through UseStatusCodePages, so all errors share one format.
            options.SuppressMapClientErrors = true;
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value is { Errors.Count: > 0 })
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

                // System.Text.Json reports unreadable bodies under keys that start with "$".
                var malformed = errors.Keys.Any(key => key.StartsWith('$')) || errors.ContainsKey("request");
                var problem = malformed
                    ? Problems.Create(
                        context.HttpContext, 400, ErrorCodes.MalformedRequest, "Malformed request",
                        "The request body could not be read.")
                    : Problems.Create(
                        context.HttpContext, 400, ErrorCodes.ValidationError, "Validation failed",
                        "One or more fields are invalid.", errors);
                return new ObjectResult(problem)
                {
                    StatusCode = 400,
                    ContentTypes = { "application/problem+json" },
                };
            };
        });

    var app = builder.Build();

    // Outermost, so the log records the final status set by the error handling below.
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseStatusCodePages(context => Problems.WriteEmptyStatusAsync(context.HttpContext));
    app.UseRouting();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

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
    }).AllowAnonymous();

    using (var scope = app.Services.CreateScope())
    {
        var admin = app.Configuration.GetSection("Admin");
        await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().EnsureAdminExistsAsync(
            new AdminAccountOptions(
                admin["Email"] ?? throw new InvalidOperationException("Admin:Email is not configured."),
                admin["Password"] ?? throw new InvalidOperationException("Admin:Password is not configured.")),
            app.Lifetime.ApplicationStopping);
    }

    app.Run();
    return 0;
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    // Otherwise the runtime aborts, which surfaces as a misleading exit code 139 when running as PID 1.
    Console.Error.WriteLine($"API failed to start: {exception.GetType().Name}: {exception.Message}");
    return 1;
}

public partial class Program;
