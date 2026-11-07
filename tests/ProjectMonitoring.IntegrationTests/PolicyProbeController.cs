using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectMonitoring.Api.Security;

namespace ProjectMonitoring.IntegrationTests;

/// <summary>Role-protected endpoints that exist only in tests, until the real ones are added.</summary>
[ApiController]
[Route("test")]
public sealed class PolicyProbeController : ControllerBase
{
    [HttpGet("admin")]
    [Authorize(Policy = Policies.AdminOnly)]
    public IActionResult Admin() => Ok();

    [HttpGet("manager")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public IActionResult Manager() => Ok();

    [HttpGet("boom")]
    public IActionResult Boom() => throw new InvalidOperationException("secret internal detail");

    [HttpGet("database-outage")]
    public IActionResult DatabaseOutage() =>
        throw new InvalidOperationException("wrapper", new FakeDbException(isTransient: true));

    [HttpGet("database-error")]
    public IActionResult DatabaseError() =>
        throw new InvalidOperationException("wrapper", new FakeDbException(isTransient: false));

    // TestServer does not enforce Kestrel's body limit, so these raise what Kestrel would.
    [HttpGet("payload-too-large")]
    public IActionResult PayloadTooLarge() =>
        throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);

    [HttpGet("bad-request")]
    public IActionResult BadRequestBody() =>
        throw new BadHttpRequestException("Unexpected end of request content.", StatusCodes.Status400BadRequest);
}
