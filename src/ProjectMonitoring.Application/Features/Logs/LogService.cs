using System.Text.Json;
using FluentValidation;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features.Logs;

public sealed class LogService(ISystemLogRepository logs, IValidator<LogListQuery> validator)
{
    public async Task<PagedResult<SystemLogDto>> ListAsync(LogListQuery query, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(query, cancellationToken);
        return (await logs.ListAsync(query, cancellationToken)).Map(log => new SystemLogDto(
            log.Id,
            log.Level.ToCode(),
            log.EventType,
            log.Message,
            log.HttpMethod,
            log.Path,
            log.StatusCode,
            log.DurationMs,
            log.TraceId,
            log.UserId,
            log.Details is null ? null : JsonDocument.Parse(log.Details).RootElement.Clone(),
            log.CreatedAt));
    }
}
