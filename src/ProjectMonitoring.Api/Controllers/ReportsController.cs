using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMonitoring.Application.Features;

namespace ProjectMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = Policies.ManagerOrAdmin)]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet(ReportService.DepartmentProgress)]
    public Task<IReadOnlyList<DepartmentProgressRow>> DepartmentProgress(CancellationToken cancellationToken) =>
        reports.GetDepartmentProgressAsync(cancellationToken);

    [HttpGet(ReportService.ProjectSummary)]
    public Task<IReadOnlyList<ProjectSummaryRow>> ProjectSummary(
        [FromQuery] ReportQuery query, CancellationToken cancellationToken) =>
        reports.GetProjectSummaryAsync(query, cancellationToken);

    [HttpGet(ReportService.UserWorkload)]
    public Task<IReadOnlyList<UserWorkloadRow>> UserWorkload(
        [FromQuery] ReportQuery query, CancellationToken cancellationToken) =>
        reports.GetUserWorkloadAsync(query, cancellationToken);

    [HttpGet("{report}/export")]
    public async Task<IActionResult> Export(
        string report, [FromQuery] ExportQuery query, CancellationToken cancellationToken)
    {
        var file = await reports.ExportAsync(report, query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
