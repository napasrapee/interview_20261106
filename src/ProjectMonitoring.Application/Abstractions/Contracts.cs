using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Abstractions;

public interface IDepartmentRepository
{
    Task<Department?> FindAsync(long id, CancellationToken cancellationToken);
    Task<PagedResult<Department>> ListAsync(PageQuery query, CancellationToken cancellationToken);
    void Add(Department department);
    void Remove(Department department);
}

public interface IUserRepository
{
    Task<User?> FindAsync(long id, CancellationToken cancellationToken);
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<PagedResult<User>> ListAsync(UserListQuery query, CancellationToken cancellationToken);
    void Add(User user);
    void Remove(User user);
}

public interface IProjectRepository
{
    Task<Project?> FindAsync(long id, CancellationToken cancellationToken);
    Task<PagedResult<Project>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken);
    void Add(Project project);
    void Remove(Project project);
}

public interface IWorkTaskRepository
{
    Task<WorkTask?> FindAsync(long id, CancellationToken cancellationToken);
    Task<PagedResult<WorkTask>> ListAsync(TaskListQuery query, DateOnly today, CancellationToken cancellationToken);
    void Add(WorkTask task);
    void Remove(WorkTask task);
}

public interface IUnitOfWork
{
    /// <summary>Throws <see cref="ConflictException"/> on unique or foreign key violations.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Takes the same time with or without a hash, so response timing does not reveal
    /// whether an account exists. Always returns false when <paramref name="passwordHash"/> is null.
    /// </summary>
    bool Verify(string password, string? passwordHash);
}

public interface ITokenIssuer
{
    AccessToken Issue(User user);
}

public sealed record AccessToken(string Value, int ExpiresInSeconds);

public interface ICurrentUser
{
    long? UserId { get; }
    UserRole? Role { get; }
}
