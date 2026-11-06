using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentValidation;
using ProjectMonitoring.Application.Common;

namespace ProjectMonitoring.Application.Features;

public sealed record DepartmentProgressRow(
    long DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    long ProjectCount,
    long TotalTasks,
    long DoneTasks,
    long InProgressTasks,
    long TodoTasks,
    long OverdueTasks,
    decimal ProgressPercent,
    long ProgressRank);

public sealed record ProjectSummaryRow(
    long ProjectId,
    string ProjectCode,
    string ProjectName,
    string Status,
    string DepartmentCode,
    string? OwnerName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? Budget,
    long TotalTasks,
    long DoneTasks,
    long InProgressTasks,
    long TodoTasks,
    long OverdueTasks,
    decimal ProgressPercent);

public sealed record UserWorkloadRow(
    long UserId,
    string FullName,
    string Email,
    string DepartmentCode,
    bool IsActive,
    long TotalTasks,
    long DoneTasks,
    long InProgressTasks,
    long TodoTasks,
    long OpenTasks,
    long OverdueTasks);

public interface IReportQueries
{
    Task<IReadOnlyList<DepartmentProgressRow>> GetDepartmentProgressAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectSummaryRow>> GetProjectSummaryAsync(long? departmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserWorkloadRow>> GetUserWorkloadAsync(long? departmentId, CancellationToken cancellationToken);
}

public sealed record ReportQuery(long? DepartmentId = null);

public sealed record ExportQuery(string? Format = null, long? DepartmentId = null);

public sealed record ExportFile(byte[] Content, string ContentType, string FileName);

public sealed class ReportQueryValidator : AbstractValidator<ReportQuery>
{
    public ReportQueryValidator() =>
        RuleFor(query => query.DepartmentId).GreaterThan(0).When(query => query.DepartmentId is not null);
}

public sealed class ExportQueryValidator : AbstractValidator<ExportQuery>
{
    public ExportQueryValidator()
    {
        RuleFor(query => query.Format)
            .Must(format => format is "json" or "csv")
            .WithMessage("Format must be json or csv.");
        RuleFor(query => query.DepartmentId).GreaterThan(0).When(query => query.DepartmentId is not null);
    }
}

/// <summary>Writes RFC 4180 CSV with a UTF-8 BOM, so Excel shows Thai text correctly.</summary>
public static class CsvWriter
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@', '\t', '\r'];
    private static readonly char[] QuotedCharacters = [',', '"', '\r', '\n'];

    public static byte[] Write<T>(IEnumerable<T> rows)
    {
        var properties = typeof(T).GetProperties();
        var csv = new StringBuilder();
        AppendLine(csv, properties.Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name)));
        foreach (var row in rows)
        {
            AppendLine(csv, properties.Select(property => FormatCell(property.GetValue(row))));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static void AppendLine(StringBuilder csv, IEnumerable<string> cells) =>
        csv.Append(string.Join(',', cells)).Append("\r\n");

    private static string FormatCell(object? value) => value switch
    {
        null => "",
        string text => Quote(Neutralize(text)),
        bool flag => flag ? "true" : "false",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => Quote(Neutralize(value.ToString() ?? "")),
    };

    // Stops spreadsheets from running user-entered text as a formula.
    private static string Neutralize(string text) =>
        text.Length > 0 && FormulaPrefixes.Contains(text[0]) ? "'" + text : text;

    private static string Quote(string text) =>
        text.IndexOfAny(QuotedCharacters) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
}

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
