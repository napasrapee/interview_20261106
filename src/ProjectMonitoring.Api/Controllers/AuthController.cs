using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectMonitoring.Api.Security;
using ProjectMonitoring.Application.Features.Auth;
using ProjectMonitoring.Application.Features.Users;

namespace ProjectMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(Policies.Login)]
    public Task<LoginResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        auth.LoginAsync(request, cancellationToken);

    [HttpGet("me")]
    public Task<UserDto> Me(CancellationToken cancellationToken) =>
        auth.GetCurrentUserAsync(cancellationToken);
}
