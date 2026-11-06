using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Abstractions;

public interface IDepartmentRepository
{
    Task<Department?> FindAsync(long id, CancellationToken cancellationToken);
    Task<PagedResult<Department>> ListAsync(PageQuery query, CancellationToken cancellationToken);
    void Add(Department department);
    void Remove(Department department);
}
