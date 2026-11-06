using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Infrastructure.Security;

internal sealed class JwtTokenIssuer(JwtOptions options, TimeProvider timeProvider) : ITokenIssuer
{
    public AccessToken Issue(User user)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(options.ExpiresMinutes),
            SigningCredentials = new SigningCredentials(options.SigningKey, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("role", user.Role.ToCode()),
            ]),
        });
        return new AccessToken(token, options.ExpiresMinutes * 60);
    }
}
