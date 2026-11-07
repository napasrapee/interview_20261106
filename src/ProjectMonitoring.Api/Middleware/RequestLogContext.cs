using System.Diagnostics;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ProjectMonitoring.Api.Middleware;

/// <summary>Request values for system_logs, cut to the column sizes so an insert never fails on length.</summary>
internal static class RequestLogContext
{
    private const int MaxMethodLength = 10;
    private const int MaxPathLength = 500;
    private const int MaxTraceIdLength = 64;

    // Kestrel accepts custom methods of any length, such as VERSION-CONTROL.
    public static string? Method(HttpContext context) =>
        RequestLoggingMiddleware.Truncate(context.Request.Method, MaxMethodLength);

    public static string? Path(HttpContext context) =>
        RequestLoggingMiddleware.Truncate(context.Request.Path.Value, MaxPathLength);

    public static string? TraceId(HttpContext context) =>
        RequestLoggingMiddleware.Truncate(Activity.Current?.Id ?? context.TraceIdentifier, MaxTraceIdLength);

    public static long? UserId(HttpContext context) =>
        long.TryParse(context.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}
