using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Domain;
using ProjectMonitoring.Infrastructure.Security;

namespace ProjectMonitoring.Api;

public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string ManagerOrAdmin = nameof(ManagerOrAdmin);
    public const string Login = nameof(Login);
}

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public long? UserId =>
        long.TryParse(accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public UserRole? Role
    {
        get
        {
            var role = accessor.HttpContext?.User.FindFirstValue("role");
            return EnumCodes.IsValid<UserRole>(role) ? EnumCodes.Parse<UserRole>(role!) : null;
        }
    }
}

public static class SecurityExtensions
{
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = JwtOptions.Load(configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = jwt.SigningKey,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "role",
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            options.Events = new JwtBearerEvents
            {
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    return Problems.WriteAsync(context.HttpContext, Problems.Create(
                        context.HttpContext, 401, ErrorCodes.Unauthorized, "Unauthorized",
                        "A valid access token is required."));
                },
                OnForbidden = context => Problems.WriteAsync(context.HttpContext, Problems.Create(
                    context.HttpContext, 403, ErrorCodes.Forbidden, "Forbidden",
                    "You do not have permission to perform this action.")),
            };
        });

        services.AddAuthorizationBuilder()
            // Every endpoint requires a token unless it is explicitly marked anonymous (BR-08).
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(UserRole.Admin.ToCode()))
            .AddPolicy(Policies.ManagerOrAdmin, policy =>
                policy.RequireRole(UserRole.Admin.ToCode(), UserRole.Manager.ToCode()));

        var loginPermitLimit = configuration.GetValue("RateLimit:LoginPermitLimit", 10);
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Policies.Login, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = loginPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
            options.OnRejected = (context, cancellationToken) => new ValueTask(Problems.WriteAsync(
                context.HttpContext,
                Problems.Create(
                    context.HttpContext, 429, ErrorCodes.RateLimited, "Too many requests",
                    "Too many login attempts. Try again later.")));
        });

        return services;
    }
}
