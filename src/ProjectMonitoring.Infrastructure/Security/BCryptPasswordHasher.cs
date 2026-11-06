using ProjectMonitoring.Application.Abstractions;

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
