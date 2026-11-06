using System.Globalization;
using System.Text.Json;
using FluentValidation;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;

namespace ProjectMonitoring.Application.Features.Reports;

public sealed class ReportService(
    IReportQueries queries,
    TimeProvider timeProvider,
    IValidator<ReportQuery> validator,
    IValidator<ExportQuery> exportValidator)
{
    public const string DepartmentProgress = "department-progress";
    public const string ProjectSummary = "project-summary";
    public const string UserWorkload = "user-workload";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<DepartmentProgressRow>> GetDepartmentProgressAsync(CancellationToken cancellationToken) =>
        queries.GetDepartmentProgressAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectSummaryRow>> GetProjectSummaryAsync(
        ReportQuery query, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(query, cancellationToken);
        return await queries.GetProjectSummaryAsync(query.DepartmentId, cancellationToken);
    }

    public async Task<IReadOnlyList<UserWorkloadRow>> GetUserWorkloadAsync(
        ReportQuery query, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(query, cancellationToken);
        return await queries.GetUserWorkloadAsync(query.DepartmentId, cancellationToken);
    }

    public async Task<ExportFile> ExportAsync(string report, ExportQuery query, CancellationToken cancellationToken)
    {
        if (report is not (DepartmentProgress or ProjectSummary or UserWorkload))
        {
            throw new NotFoundException($"Report '{report}'");
        }

        await exportValidator.EnsureValidAsync(query, cancellationToken);
        var format = query.Format!;
        var content = report switch
        {
            DepartmentProgress => Serialize(await queries.GetDepartmentProgressAsync(cancellationToken), format),
            ProjectSummary => Serialize(await queries.GetProjectSummaryAsync(query.DepartmentId, cancellationToken), format),
            _ => Serialize(await queries.GetUserWorkloadAsync(query.DepartmentId, cancellationToken), format),
        };

        var date = timeProvider.GetUtcNow().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return new ExportFile(
            content,
            format == "csv" ? "text/csv; charset=utf-8" : "application/json",
            $"{report}-{date}.{format}");
    }

    private static byte[] Serialize<T>(IReadOnlyList<T> rows, string format) =>
        format == "csv" ? CsvWriter.Write(rows) : JsonSerializer.SerializeToUtf8Bytes(rows, JsonOptions);
}
