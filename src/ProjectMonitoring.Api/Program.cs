using ProjectMonitoring.Api.Errors;
using ProjectMonitoring.Api.Middleware;
using ProjectMonitoring.Api.Security;
using ProjectMonitoring.Api.Startup;
using ProjectMonitoring.Application;
using ProjectMonitoring.Infrastructure;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddSecretFiles();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApiSecurity(builder.Configuration);
    builder.Services.AddApiControllers();

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

    await app.EnsureAdminAccountAsync();

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
