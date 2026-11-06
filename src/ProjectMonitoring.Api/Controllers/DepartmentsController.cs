using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMonitoring.Api.Security;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Departments;

namespace ProjectMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/departments")]
public sealed class DepartmentsController(DepartmentService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<DepartmentDto>> List([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        service.ListAsync(query, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<DepartmentDto> Get(long id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<DepartmentDto>> Create(DepartmentRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<DepartmentDto> Update(long id, DepartmentRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:long}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
