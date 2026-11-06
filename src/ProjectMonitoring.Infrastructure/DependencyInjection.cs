using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Infrastructure.Health;
using ProjectMonitoring.Infrastructure.Persistence;
using ProjectMonitoring.Infrastructure.Security;

namespace ProjectMonitoring.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IDatabaseHealthCheck, NpgsqlDatabaseHealthCheck>();
        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IWorkTaskRepository, WorkTaskRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ProjectMonitoring.Application.Features.ISystemLogRepository, SystemLogRepository>();
        services.AddSingleton<ProjectMonitoring.Application.Features.ISystemLogWriter, SystemLogWriter>();
        services.AddSingleton<ProjectMonitoring.Application.Features.IReportQueries, ReportQueries>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton(JwtOptions.Load(configuration));
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
