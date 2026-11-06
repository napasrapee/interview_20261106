namespace ProjectMonitoring.Application.Features.Projects;

public sealed record ProjectDto(
    long Id,
    long DepartmentId,
    long? OwnerUserId,
    string Code,
    string Name,
    string? Description,
    string Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? Budget,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
