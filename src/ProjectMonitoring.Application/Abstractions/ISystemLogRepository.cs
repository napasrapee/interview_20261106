using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Logs;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Abstractions;

public interface ISystemLogRepository
{
    Task<PagedResult<SystemLog>> ListAsync(LogListQuery query, CancellationToken cancellationToken);
}
