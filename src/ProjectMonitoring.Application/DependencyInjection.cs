using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProjectMonitoring.Application.Features;

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
        services.AddScoped<AuthService>();
        services.AddScoped<AdminBootstrapper>();
        return services;
    }
}
