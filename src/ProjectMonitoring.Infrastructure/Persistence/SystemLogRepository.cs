using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Logs;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Infrastructure.Persistence;

internal sealed class SystemLogRepository(AppDbContext db) : ISystemLogRepository
{
    public Task<PagedResult<SystemLog>> ListAsync(LogListQuery query, CancellationToken cancellationToken)
    {
        var logs = db.SystemLogs.AsQueryable();
        if (query.Level is not null)
        {
            var level = EnumCodes.Parse<SystemLogLevel>(query.Level);
            logs = logs.Where(log => log.Level == level);
        }

        if (query.EventType is not null)
        {
            logs = logs.Where(log => log.EventType == query.EventType);
        }

        if (query.From is { } from)
        {
            var utcFrom = from.ToUniversalTime();
            logs = logs.Where(log => log.CreatedAt >= utcFrom);
        }

        if (query.To is { } to)
        {
            var utcTo = to.ToUniversalTime();
            logs = logs.Where(log => log.CreatedAt <= utcTo);
        }

        return logs
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .ToPagedResultAsync(query, cancellationToken);
    }
}
