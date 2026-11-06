using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Projects;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Infrastructure.Persistence;

internal sealed class ProjectRepository(AppDbContext db) : IProjectRepository
{
    public async Task<Project?> FindAsync(long id, CancellationToken cancellationToken) =>
        await db.Projects.FindAsync([id], cancellationToken);

    public Task<PagedResult<Project>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken)
    {
        var projects = db.Projects.AsQueryable();
        if (query.DepartmentId is { } departmentId)
        {
            projects = projects.Where(project => project.DepartmentId == departmentId);
        }

        if (query.Status is not null)
        {
            var status = EnumCodes.Parse<ProjectStatus>(query.Status);
            projects = projects.Where(project => project.Status == status);
        }

        return projects.OrderBy(project => project.Id).ToPagedResultAsync(query, cancellationToken);
    }

    public void Add(Project project) => db.Projects.Add(project);

    public void Remove(Project project) => db.Projects.Remove(project);
}
