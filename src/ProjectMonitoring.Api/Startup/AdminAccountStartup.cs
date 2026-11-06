using ProjectMonitoring.Application.Features.Auth;

namespace ProjectMonitoring.Api.Startup;

public static class AdminAccountStartup
{
    public static async Task EnsureAdminAccountAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var admin = app.Configuration.GetSection("Admin");
        await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().EnsureAdminExistsAsync(
            new AdminAccountOptions(
                admin["Email"] ?? throw new InvalidOperationException("Admin:Email is not configured."),
                admin["Password"] ?? throw new InvalidOperationException("Admin:Password is not configured.")),
            app.Lifetime.ApplicationStopping);
    }
}
