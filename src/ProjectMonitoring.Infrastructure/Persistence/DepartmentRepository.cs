using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Infrastructure.Persistence;

internal sealed class DepartmentRepository(AppDbContext db) : IDepartmentRepository
{
    public async Task<Department?> FindAsync(long id, CancellationToken cancellationToken) =>
        await db.Departments.FindAsync([id], cancellationToken);

    public Task<PagedResult<Department>> ListAsync(PageQuery query, CancellationToken cancellationToken) =>
        db.Departments.OrderBy(department => department.Id).ToPagedResultAsync(query, cancellationToken);

    public void Add(Department department) => db.Departments.Add(department);

    public void Remove(Department department) => db.Departments.Remove(department);
}
