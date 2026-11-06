using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMonitoring.Api.Security;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Users;

namespace ProjectMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(UserService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public Task<PagedResult<UserDto>> List([FromQuery] UserListQuery query, CancellationToken cancellationToken) =>
        service.ListAsync(query, cancellationToken);

    [HttpGet("{id:long}")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public Task<UserDto> Get(long id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<UserDto>> Create(UserRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<UserDto> Update(long id, UserRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:long}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
