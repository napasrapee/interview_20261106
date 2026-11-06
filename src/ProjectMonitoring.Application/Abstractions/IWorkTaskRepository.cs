using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Tasks;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Abstractions;

public interface IWorkTaskRepository
{
    Task<WorkTask?> FindAsync(long id, CancellationToken cancellationToken);
    Task<PagedResult<WorkTask>> ListAsync(TaskListQuery query, DateOnly today, CancellationToken cancellationToken);
    void Add(WorkTask task);
    void Remove(WorkTask task);
}
