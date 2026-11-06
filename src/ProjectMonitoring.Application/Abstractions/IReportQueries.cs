using ProjectMonitoring.Application.Features.Reports;

namespace ProjectMonitoring.Application.Abstractions;

public interface IReportQueries
{
    Task<IReadOnlyList<DepartmentProgressRow>> GetDepartmentProgressAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectSummaryRow>> GetProjectSummaryAsync(long? departmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserWorkloadRow>> GetUserWorkloadAsync(long? departmentId, CancellationToken cancellationToken);
}
