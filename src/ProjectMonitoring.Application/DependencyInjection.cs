using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProjectMonitoring.Application.Features.Auth;
using ProjectMonitoring.Application.Features.Departments;
using ProjectMonitoring.Application.Features.Logs;
using ProjectMonitoring.Application.Features.Projects;
using ProjectMonitoring.Application.Features.Reports;
using ProjectMonitoring.Application.Features.Tasks;
using ProjectMonitoring.Application.Features.Users;

namespace ProjectMonitoring.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<DepartmentService>(ServiceLifetime.Singleton);
        services.AddScoped<DepartmentService>();
        services.AddScoped<UserService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<TaskService>();
        services.AddScoped<ReportService>();
        services.AddScoped<LogService>();
        services.AddScoped<AuthService>();
        services.AddScoped<AdminBootstrapper>();
        return services;
    }
}
