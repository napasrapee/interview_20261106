using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ProjectMonitoring.Infrastructure.Security;

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
