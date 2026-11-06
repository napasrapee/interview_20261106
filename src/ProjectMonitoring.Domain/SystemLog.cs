namespace ProjectMonitoring.Domain;

public sealed class SystemLog
{
    private SystemLog()
    {
    }

    public SystemLog(SystemLogLevel level, string eventType, string message)
    {
        Level = level;
        EventType = eventType;
        Message = message;
    }

    public long Id { get; private set; }
    public SystemLogLevel Level { get; private set; }
    public string EventType { get; private set; } = "";
    public string Message { get; private set; } = "";
    public string? HttpMethod { get; init; }
    public string? Path { get; init; }
    public int? StatusCode { get; init; }
    public int? DurationMs { get; init; }
    public string? TraceId { get; init; }
    public long? UserId { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset CreatedAt { get; private set; }
}
