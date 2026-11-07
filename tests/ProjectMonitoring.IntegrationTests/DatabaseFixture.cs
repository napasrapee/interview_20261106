using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectMonitoring.Application;
using ProjectMonitoring.Infrastructure;
using Testcontainers.PostgreSql;

namespace ProjectMonitoring.IntegrationTests;

/// <summary>Runs one PostgreSQL container initialised from the real db/init scripts.</summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithBindMount(FindInitDirectory(), "/docker-entrypoint-initdb.d", DotNet.Testcontainers.Configurations.AccessMode.ReadOnly)
        .Build();

    public ServiceProvider Services { get; private set; } = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _container.GetConnectionString(),
                ["Jwt:Secret"] = new string('x', 32),
                ["Jwt:Issuer"] = "test",
                ["Jwt:Audience"] = "test",
            })
            .Build();
        Services = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        await _container.DisposeAsync();
    }

    private static string FindInitDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "db", "init");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("db/init was not found.");
    }
}
