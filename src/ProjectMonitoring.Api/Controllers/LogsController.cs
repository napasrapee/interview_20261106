using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features;

namespace ProjectMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/logs")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class LogsController(LogService logs) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SystemLogDto>> List([FromQuery] LogListQuery query, CancellationToken cancellationToken) =>
        logs.ListAsync(query, cancellationToken);
}
