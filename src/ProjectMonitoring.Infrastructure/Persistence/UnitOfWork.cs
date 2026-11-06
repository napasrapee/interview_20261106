using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;

namespace ProjectMonitoring.Infrastructure.Persistence;

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
