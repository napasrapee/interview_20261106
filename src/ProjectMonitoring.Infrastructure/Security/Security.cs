using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Infrastructure.Security;

internal sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    // Verified when an account has no hash, to keep timing the same as a real check.
    private static readonly Lazy<string> DummyHash =
        new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), WorkFactor));

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string? passwordHash)
    {
        var matches = BCrypt.Net.BCrypt.Verify(password, passwordHash ?? DummyHash.Value);
        return passwordHash is not null && matches;
    }
}

public sealed record JwtOptions(string Secret, string Issuer, string Audience, int ExpiresMinutes)
{
    private const int MinimumSecretBytes = 32;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Secret));

    /// <summary>Fails at startup rather than running with a weak or missing secret.</summary>
    public static JwtOptions Load(IConfiguration configuration)
    {
        var section = configuration.GetSection("Jwt");
        var options = new JwtOptions(
            section["Secret"] ?? "",
            section["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured."),
            section["Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured."),
            int.TryParse(section["ExpiresMinutes"], out var minutes) && minutes > 0 ? minutes : 60);

        if (Encoding.UTF8.GetByteCount(options.Secret) < MinimumSecretBytes)
        {
            throw new InvalidOperationException($"Jwt:Secret must be at least {MinimumSecretBytes} bytes.");
        }

        return options;
    }
}

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
