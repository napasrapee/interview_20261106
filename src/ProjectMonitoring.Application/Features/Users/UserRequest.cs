namespace ProjectMonitoring.Application.Features.Users;

public sealed record UserRequest(
    long DepartmentId,
    string FullName,
    string Email,
    string? Password = null,
    string? Role = null,
    bool? IsActive = null);
