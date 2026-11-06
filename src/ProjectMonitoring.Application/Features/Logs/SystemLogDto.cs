using System.Text.Json;

namespace ProjectMonitoring.Application.Features.Logs;

public sealed record SystemLogDto(
    long Id,
    string Level,
    string EventType,
    string Message,
    string? HttpMethod,
    string? Path,
    int? StatusCode,
    int? DurationMs,
    string? TraceId,
    long? UserId,
    JsonElement? Details,
    DateTimeOffset CreatedAt);
