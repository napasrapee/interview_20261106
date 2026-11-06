namespace ProjectMonitoring.Application.Features.Reports;

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
