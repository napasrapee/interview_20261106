using ProjectMonitoring.Application.Common;

namespace ProjectMonitoring.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>Throws <see cref="ConflictException"/> on unique or foreign key violations.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
