using System.Diagnostics;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ProjectMonitoring.Api.Middleware;

internal static class RequestLogContext
{
    private const int MaxTraceIdLength = 64;

    public static string? TraceId(HttpContext context) =>
        RequestLoggingMiddleware.Truncate(Activity.Current?.Id ?? context.TraceIdentifier, MaxTraceIdLength);

    public static long? UserId(HttpContext context) =>
        long.TryParse(context.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}
