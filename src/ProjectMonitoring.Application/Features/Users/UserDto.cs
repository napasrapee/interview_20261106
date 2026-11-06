namespace ProjectMonitoring.Application.Features.Users;

public sealed record UserDto(
    long Id,
    long DepartmentId,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
