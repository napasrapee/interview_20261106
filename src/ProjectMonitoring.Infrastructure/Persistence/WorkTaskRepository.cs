using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Tasks;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Infrastructure.Persistence;

internal sealed class WorkTaskRepository(AppDbContext db) : IWorkTaskRepository
{
    public async Task<WorkTask?> FindAsync(long id, CancellationToken cancellationToken) =>
        await db.Tasks.FindAsync([id], cancellationToken);

    public Task<PagedResult<WorkTask>> ListAsync(TaskListQuery query, DateOnly today, CancellationToken cancellationToken)
    {
        var tasks = db.Tasks.AsQueryable();
        if (query.ProjectId is { } projectId)
        {
            tasks = tasks.Where(task => task.ProjectId == projectId);
        }

        if (query.AssigneeUserId is { } assigneeUserId)
        {
            tasks = tasks.Where(task => task.AssigneeUserId == assigneeUserId);
        }

        if (query.Status is not null)
        {
            var status = EnumCodes.Parse<WorkTaskStatus>(query.Status);
            tasks = tasks.Where(task => task.Status == status);
        }

        if (query.Overdue is { } overdue)
        {
            // Same rule as WorkTask.IsOverdue (RULE-04), expressed so it runs in SQL.
            tasks = overdue
                ? tasks.Where(task => task.Status != WorkTaskStatus.Done && task.DueDate < today)
                : tasks.Where(task => task.Status == WorkTaskStatus.Done || task.DueDate == null || task.DueDate >= today);
        }

        return tasks.OrderBy(task => task.Id).ToPagedResultAsync(query, cancellationToken);
    }

    public void Add(WorkTask task) => db.Tasks.Add(task);

    public void Remove(WorkTask task) => db.Tasks.Remove(task);
}
