namespace ProjectMonitoring.Application.Features.Departments;

public sealed record DepartmentDto(
    long Id, string Code, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
