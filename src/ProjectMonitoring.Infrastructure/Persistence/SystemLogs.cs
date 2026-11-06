using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Infrastructure.Persistence;

internal sealed class SystemLogRepository(AppDbContext db) : ISystemLogRepository
{
    public Task<PagedResult<SystemLog>> ListAsync(LogListQuery query, CancellationToken cancellationToken)
    {
        var logs = db.SystemLogs.AsQueryable();
        if (query.Level is not null)
        {
            var level = EnumCodes.Parse<SystemLogLevel>(query.Level);
            logs = logs.Where(log => log.Level == level);
        }

        if (query.EventType is not null)
        {
            logs = logs.Where(log => log.EventType == query.EventType);
        }

        if (query.From is { } from)
        {
            var utcFrom = from.ToUniversalTime();
            logs = logs.Where(log => log.CreatedAt >= utcFrom);
        }

        if (query.To is { } to)
        {
            var utcTo = to.ToUniversalTime();
            logs = logs.Where(log => log.CreatedAt <= utcTo);
        }

        return logs
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .ToPagedResultAsync(query, cancellationToken);
    }
}

/// <summary>
/// Uses its own connection rather than the request's DbContext, which may hold failed changes.
/// Falls back to stdout, so a database outage is still visible in the container logs.
/// </summary>
internal sealed class SystemLogWriter(NpgsqlDataSource dataSource, ILogger<SystemLogWriter> logger) : ISystemLogWriter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private const string InsertSql = """
        INSERT INTO system_logs
          (level, event_type, message, http_method, path, status_code, duration_ms, trace_id, user_id, details)
        VALUES
          (@level, @event_type, @message, @http_method, @path, @status_code, @duration_ms, @trace_id, @user_id, @details)
        """;

    public async Task WriteAsync(SystemLog log)
    {
        try
        {
            using var timeout = new CancellationTokenSource(Timeout);
            await using var command = dataSource.CreateCommand(InsertSql);
            command.Parameters.Add(new NpgsqlParameter<string>("level", log.Level.ToCode()));
            command.Parameters.Add(new NpgsqlParameter<string>("event_type", log.EventType));
            command.Parameters.Add(new NpgsqlParameter<string>("message", log.Message));
            command.Parameters.Add(new NpgsqlParameter<string?>("http_method", NpgsqlDbType.Varchar) { TypedValue = log.HttpMethod });
            command.Parameters.Add(new NpgsqlParameter<string?>("path", NpgsqlDbType.Varchar) { TypedValue = log.Path });
            command.Parameters.Add(new NpgsqlParameter<int?>("status_code", log.StatusCode));
            command.Parameters.Add(new NpgsqlParameter<int?>("duration_ms", log.DurationMs));
            command.Parameters.Add(new NpgsqlParameter<string?>("trace_id", NpgsqlDbType.Varchar) { TypedValue = log.TraceId });
            command.Parameters.Add(new NpgsqlParameter<long?>("user_id", log.UserId));
            command.Parameters.Add(new NpgsqlParameter<string?>("details", NpgsqlDbType.Jsonb) { TypedValue = log.Details });
            await command.ExecuteNonQueryAsync(timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Could not write system log {EventType}: {Error}", log.EventType, exception.GetType().Name);
        }
    }
}
