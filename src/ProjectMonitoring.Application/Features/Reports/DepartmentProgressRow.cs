namespace ProjectMonitoring.Application.Features.Reports;

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
