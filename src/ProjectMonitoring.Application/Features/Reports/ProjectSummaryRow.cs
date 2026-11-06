namespace ProjectMonitoring.Application.Features.Reports;

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
