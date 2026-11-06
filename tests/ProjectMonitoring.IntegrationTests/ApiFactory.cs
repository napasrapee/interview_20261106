using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectMonitoring.IntegrationTests;

public static class ApiFactory
{
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "admin-password-1";
    public const string Secret = "integration-test-secret-integration-test-secret";

    public static WebApplicationFactory<Program> Create(
        string connectionString, int loginLimit = 100_000, Assembly? extraControllers = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Jwt:Secret", Secret);
            builder.UseSetting("Jwt:Issuer", "test-issuer");
            builder.UseSetting("Jwt:Audience", "test-audience");
            builder.UseSetting("Jwt:ExpiresMinutes", "60");
            builder.UseSetting("Admin:Email", AdminEmail);
            builder.UseSetting("Admin:Password", AdminPassword);
            builder.UseSetting("RateLimit:LoginPermitLimit", loginLimit.ToString());
            if (extraControllers is not null)
            {
                builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(extraControllers));
            }
        });
}
