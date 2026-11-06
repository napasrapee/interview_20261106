using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMonitoring.Api.Security;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Projects;

namespace ProjectMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/projects")]
public sealed class ProjectsController(ProjectService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProjectDto>> List([FromQuery] ProjectListQuery query, CancellationToken cancellationToken) =>
        service.ListAsync(query, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<ProjectDto> Get(long id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<ActionResult<ProjectDto>> Create(ProjectRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public Task<ProjectDto> Update(long id, ProjectRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:long}")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
