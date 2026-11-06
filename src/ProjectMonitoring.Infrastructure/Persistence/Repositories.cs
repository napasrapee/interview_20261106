using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Infrastructure.Persistence;

internal static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PageQuery page, CancellationToken cancellationToken)
        where T : class
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }
}

internal sealed class DepartmentRepository(AppDbContext db) : IDepartmentRepository
{
    public async Task<Department?> FindAsync(long id, CancellationToken cancellationToken) =>
        await db.Departments.FindAsync([id], cancellationToken);

    public Task<PagedResult<Department>> ListAsync(PageQuery query, CancellationToken cancellationToken) =>
        db.Departments.OrderBy(department => department.Id).ToPagedResultAsync(query, cancellationToken);

    public void Add(Department department) => db.Departments.Add(department);

    public void Remove(Department department) => db.Departments.Remove(department);
}

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<User?> FindAsync(long id, CancellationToken cancellationToken) =>
        await db.Users.FindAsync([id], cancellationToken);

    public Task<PagedResult<User>> ListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsQueryable();
        if (query.DepartmentId is { } departmentId)
        {
            users = users.Where(user => user.DepartmentId == departmentId);
        }

        if (query.Role is not null)
        {
            var role = EnumCodes.Parse<UserRole>(query.Role);
            users = users.Where(user => user.Role == role);
        }

        return users.OrderBy(user => user.Id).ToPagedResultAsync(query, cancellationToken);
    }

    public void Add(User user) => db.Users.Add(user);

    public void Remove(User user) => db.Users.Remove(user);
}

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

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            throw new ConflictException("A record with the same unique value already exists.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation
        })
        {
            throw new ConflictException(
                "The record references missing data or is still referenced by other records.", exception);
        }
    }
}
