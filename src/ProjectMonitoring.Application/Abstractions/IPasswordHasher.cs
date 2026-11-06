namespace ProjectMonitoring.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Takes the same time with or without a hash, so response timing does not reveal
    /// whether an account exists. Always returns false when <paramref name="passwordHash"/> is null.
    /// </summary>
    bool Verify(string password, string? passwordHash);
}
