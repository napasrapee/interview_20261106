using Npgsql;
using ProjectMonitoring.Application.Abstractions;

namespace ProjectMonitoring.Infrastructure.Health;

internal sealed class NpgsqlDatabaseHealthCheck(NpgsqlDataSource dataSource) : IDatabaseHealthCheck
{
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or OperationCanceledException)
        {
            return false;
        }
    }
}
