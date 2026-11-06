using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Projects;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Abstractions;

public interface IProjectRepository
{
    Task<Project?> FindAsync(long id, CancellationToken cancellationToken);
    Task<PagedResult<Project>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken);
    void Add(Project project);
    void Remove(Project project);
}
