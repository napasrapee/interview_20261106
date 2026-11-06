namespace ProjectMonitoring.Domain;

public sealed class User
{
    private User()
    {
    }

    public User(long departmentId, string fullName, string email, UserRole role, bool isActive) =>
        Update(departmentId, fullName, email, role, isActive);

    public long Id { get; private set; }
    public long DepartmentId { get; private set; }
    public string FullName { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string? PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>RULE-09: accounts that are inactive or have no password cannot log in.</summary>
    public bool CanLogIn => IsActive && PasswordHash is not null;

    public void Update(long departmentId, string fullName, string email, UserRole role, bool isActive)
    {
        DepartmentId = departmentId;
        FullName = fullName;
        Email = email;
        Role = role;
        IsActive = isActive;
    }

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;
}
