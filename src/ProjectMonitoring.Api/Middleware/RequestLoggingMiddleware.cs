using System.Diagnostics;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Api.Middleware;

/// <summary>
/// Records each request in system_logs. Only method, path and status are kept: never bodies,
/// query strings or headers, so passwords and tokens cannot reach the table.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ISystemLogWriter writer)
{
    private const int MaxPathLength = 500;

    public async Task InvokeAsync(HttpContext context)
    {
        // Container healthchecks call this every few seconds, which would bury real events.
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            var status = context.Response.StatusCode;
            await writer.WriteAsync(new SystemLog(LevelFor(status), "http_request", $"{context.Request.Method} responded {status}")
            {
                HttpMethod = context.Request.Method,
                Path = Truncate(context.Request.Path.Value, MaxPathLength),
                StatusCode = status,
                DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue),
                TraceId = RequestLogContext.TraceId(context),
                UserId = RequestLogContext.UserId(context),
            });
        }
    }

    private static SystemLogLevel LevelFor(int status) => status switch
    {
        >= 500 => SystemLogLevel.Error,
        >= 400 => SystemLogLevel.Warning,
        _ => SystemLogLevel.Info,
    };

    internal static string? Truncate(string? value, int length) =>
        value is not null && value.Length > length ? value[..length] : value;
}
