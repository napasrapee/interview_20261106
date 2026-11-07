using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Api.Security;

/// <summary>
/// Re-reads the account on every authenticated request, so deactivating, deleting or changing the
/// role of a user takes effect immediately instead of when the token expires.
/// </summary>
internal static class CurrentAccountValidation
{
    public static async Task ApplyAsync(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity
            || !long.TryParse(identity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
        {
            context.Fail("The token has no valid subject.");
            return;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var user = await users.FindAsync(userId, context.HttpContext.RequestAborted);
        if (user is not { IsActive: true })
        {
            context.Fail("The account no longer exists or is inactive.");
            return;
        }

        // The role in the token may be out of date, so authorization uses the stored one.
        foreach (var claim in identity.FindAll("role").ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim("role", user.Role.ToCode()));
    }
}
