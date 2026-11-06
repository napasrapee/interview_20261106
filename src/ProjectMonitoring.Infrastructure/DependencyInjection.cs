using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Infrastructure.Health;

namespace ProjectMonitoring.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IDatabaseHealthCheck, NpgsqlDatabaseHealthCheck>();
        return services;
    }
}
