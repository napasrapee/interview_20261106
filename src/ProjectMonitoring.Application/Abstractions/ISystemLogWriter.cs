using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Abstractions;

/// <summary>Best effort: a failure to write a log must never fail the request being logged.</summary>
public interface ISystemLogWriter
{
    Task WriteAsync(SystemLog log);
}
