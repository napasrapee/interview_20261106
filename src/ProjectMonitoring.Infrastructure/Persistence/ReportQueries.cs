using Npgsql;
using ProjectMonitoring.Application.Features;

namespace ProjectMonitoring.Infrastructure.Persistence;

/// <summary>Report SQL: joins and aggregates are clearer and faster written by hand than through EF.</summary>
internal sealed class ReportQueries(NpgsqlDataSource dataSource) : IReportQueries
{
    private const string DepartmentProgressSql = """
        WITH department_stats AS (
          SELECT p.department_id,
                 COUNT(DISTINCT p.id)                                 AS project_count,
                 COUNT(t.id)                                          AS total_tasks,
                 COUNT(t.id) FILTER (WHERE t.status = 'done')         AS done_tasks,
                 COUNT(t.id) FILTER (WHERE t.status = 'in_progress')  AS in_progress_tasks,
                 COUNT(t.id) FILTER (WHERE t.status = 'todo')         AS todo_tasks,
                 COUNT(t.id) FILTER (WHERE t.status <> 'done'
                                       AND t.due_date < (now() AT TIME ZONE 'UTC')::date) AS overdue_tasks
          FROM projects p
          LEFT JOIN tasks t ON t.project_id = p.id
          WHERE p.status <> 'cancelled'
          GROUP BY p.department_id
        ), department_progress AS (
          SELECT d.id AS department_id, d.code AS department_code, d.name AS department_name,
                 COALESCE(s.project_count, 0)     AS project_count,
                 COALESCE(s.total_tasks, 0)       AS total_tasks,
                 COALESCE(s.done_tasks, 0)        AS done_tasks,
                 COALESCE(s.in_progress_tasks, 0) AS in_progress_tasks,
                 COALESCE(s.todo_tasks, 0)        AS todo_tasks,
                 COALESCE(s.overdue_tasks, 0)     AS overdue_tasks,
                 COALESCE(ROUND(100.0 * s.done_tasks / NULLIF(s.total_tasks, 0), 2), 0)::numeric(5, 2) AS progress_percent
          FROM departments d
          LEFT JOIN department_stats s ON s.department_id = d.id
        )
        SELECT department_id, department_code, department_name, project_count, total_tasks, done_tasks,
               in_progress_tasks, todo_tasks, overdue_tasks, progress_percent,
               RANK() OVER (ORDER BY progress_percent DESC) AS progress_rank
        FROM department_progress
        ORDER BY progress_rank, department_code
        """;

    private const string ProjectSummarySql = """
        SELECT p.id, p.code, p.name, p.status, d.code, u.full_name, p.start_date, p.end_date, p.budget,
               COUNT(t.id)                                         AS total_tasks,
               COUNT(t.id) FILTER (WHERE t.status = 'done')        AS done_tasks,
               COUNT(t.id) FILTER (WHERE t.status = 'in_progress') AS in_progress_tasks,
               COUNT(t.id) FILTER (WHERE t.status = 'todo')        AS todo_tasks,
               COUNT(t.id) FILTER (WHERE t.status <> 'done'
                                     AND t.due_date < (now() AT TIME ZONE 'UTC')::date) AS overdue_tasks,
               COALESCE(ROUND(100.0 * COUNT(t.id) FILTER (WHERE t.status = 'done')
                              / NULLIF(COUNT(t.id), 0), 2), 0)::numeric(5, 2) AS progress_percent
        FROM projects p
        JOIN departments d ON d.id = p.department_id
        LEFT JOIN users u ON u.id = p.owner_user_id
        LEFT JOIN tasks t ON t.project_id = p.id
        WHERE p.status <> 'cancelled'
          AND (@department_id::bigint IS NULL OR p.department_id = @department_id)
        GROUP BY p.id, d.code, u.full_name
        ORDER BY p.code
        """;

    private const string UserWorkloadSql = """
        SELECT u.id, u.full_name, u.email, d.code, u.is_active,
               COUNT(t.id)                                          AS total_tasks,
               COUNT(t.id) FILTER (WHERE t.status = 'done')         AS done_tasks,
               COUNT(t.id) FILTER (WHERE t.status = 'in_progress')  AS in_progress_tasks,
               COUNT(t.id) FILTER (WHERE t.status = 'todo')         AS todo_tasks,
               COUNT(t.id) FILTER (WHERE t.status <> 'done')        AS open_tasks,
               COUNT(t.id) FILTER (WHERE t.status <> 'done'
                                     AND t.due_date < (now() AT TIME ZONE 'UTC')::date) AS overdue_tasks
        FROM users u
        JOIN departments d ON d.id = u.department_id
        LEFT JOIN (
          SELECT t.* FROM tasks t
          JOIN projects p ON p.id = t.project_id
          WHERE p.status <> 'cancelled'
        ) t ON t.assignee_user_id = u.id
        WHERE @department_id::bigint IS NULL OR u.department_id = @department_id
        GROUP BY u.id, d.code
        ORDER BY open_tasks DESC, u.full_name
        """;

    public Task<IReadOnlyList<DepartmentProgressRow>> GetDepartmentProgressAsync(CancellationToken cancellationToken) =>
        ReadAsync(DepartmentProgressSql, null, reader => new DepartmentProgressRow(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt64(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetInt64(7),
            reader.GetInt64(8),
            reader.GetDecimal(9),
            reader.GetInt64(10)), cancellationToken);

    public Task<IReadOnlyList<ProjectSummaryRow>> GetProjectSummaryAsync(long? departmentId, CancellationToken cancellationToken) =>
        ReadAsync(ProjectSummarySql, departmentId, reader => new ProjectSummaryRow(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateOnly>(7),
            reader.IsDBNull(8) ? null : reader.GetDecimal(8),
            reader.GetInt64(9),
            reader.GetInt64(10),
            reader.GetInt64(11),
            reader.GetInt64(12),
            reader.GetInt64(13),
            reader.GetDecimal(14)), cancellationToken);

    public Task<IReadOnlyList<UserWorkloadRow>> GetUserWorkloadAsync(long? departmentId, CancellationToken cancellationToken) =>
        ReadAsync(UserWorkloadSql, departmentId, reader => new UserWorkloadRow(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetInt64(7),
            reader.GetInt64(8),
            reader.GetInt64(9),
            reader.GetInt64(10)), cancellationToken);

    private async Task<IReadOnlyList<T>> ReadAsync<T>(
        string sql, long? departmentId, Func<NpgsqlDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(sql);
        if (sql.Contains("@department_id", StringComparison.Ordinal))
        {
            command.Parameters.Add(new NpgsqlParameter<long?>("department_id", departmentId));
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }
}
