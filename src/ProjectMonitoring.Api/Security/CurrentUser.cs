using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Api.Security;

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public long? UserId =>
        long.TryParse(accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public UserRole? Role
    {
        get
        {
            var role = accessor.HttpContext?.User.FindFirstValue("role");
            return EnumCodes.IsValid<UserRole>(role) ? EnumCodes.Parse<UserRole>(role!) : null;
        }
    }
}
