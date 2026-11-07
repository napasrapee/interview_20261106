using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Infrastructure.Persistence;

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
            try
            {
                await InsertAsync(log, log.UserId, log.Details, timeout.Token);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation
                                                      && log.UserId is { } deletedUserId)
            {
                // The account was deleted after the request started. Keep the event, and the id, without the link.
                await InsertAsync(log, null, WithDeletedUser(log.Details, deletedUserId), timeout.Token);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning("Could not write system log {EventType}: {Error}", log.EventType, exception.GetType().Name);
        }
    }

    private async Task InsertAsync(SystemLog log, long? userId, string? details, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(InsertSql);
        command.Parameters.Add(new NpgsqlParameter<string>("level", log.Level.ToCode()));
        command.Parameters.Add(new NpgsqlParameter<string>("event_type", log.EventType));
        command.Parameters.Add(new NpgsqlParameter<string>("message", log.Message));
        command.Parameters.Add(new NpgsqlParameter<string?>("http_method", NpgsqlDbType.Varchar) { TypedValue = log.HttpMethod });
        command.Parameters.Add(new NpgsqlParameter<string?>("path", NpgsqlDbType.Varchar) { TypedValue = log.Path });
        command.Parameters.Add(new NpgsqlParameter<int?>("status_code", log.StatusCode));
        command.Parameters.Add(new NpgsqlParameter<int?>("duration_ms", log.DurationMs));
        command.Parameters.Add(new NpgsqlParameter<string?>("trace_id", NpgsqlDbType.Varchar) { TypedValue = log.TraceId });
        command.Parameters.Add(new NpgsqlParameter<long?>("user_id", userId));
        command.Parameters.Add(new NpgsqlParameter<string?>("details", NpgsqlDbType.Jsonb) { TypedValue = details });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string WithDeletedUser(string? details, long userId)
    {
        var json = (details is null ? null : JsonNode.Parse(details) as JsonObject) ?? new JsonObject();
        json["deletedUserId"] = userId;
        return json.ToJsonString();
    }
}
