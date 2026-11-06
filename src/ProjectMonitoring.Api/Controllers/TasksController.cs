using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMonitoring.Api.Security;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Tasks;

namespace ProjectMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/tasks")]
public sealed class TasksController(TaskService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<TaskDto>> List([FromQuery] TaskListQuery query, CancellationToken cancellationToken) =>
        service.ListAsync(query, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<TaskDto> Get(long id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<ActionResult<TaskDto>> Create(TaskRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public Task<TaskDto> Update(long id, TaskRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    // Open to every role: the service enforces that members only change their own tasks (RULE-10).
    [HttpPatch("{id:long}/status")]
    public Task<TaskDto> ChangeStatus(long id, TaskStatusRequest request, CancellationToken cancellationToken) =>
        service.ChangeStatusAsync(id, request, cancellationToken);

    [HttpDelete("{id:long}")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
