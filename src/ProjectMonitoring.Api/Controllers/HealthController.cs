using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMonitoring.Application.Abstractions;

namespace ProjectMonitoring.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public sealed class HealthController(IDatabaseHealthCheck database) : ControllerBase
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(3);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CheckTimeout);

        var healthy = await database.IsHealthyAsync(timeout.Token);
        return StatusCode(
            healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable,
            new
            {
                status = healthy ? "healthy" : "unhealthy",
                database = healthy ? "up" : "down",
                timestamp = DateTimeOffset.UtcNow,
            });
    }
}
