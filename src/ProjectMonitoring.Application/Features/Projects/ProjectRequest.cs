namespace ProjectMonitoring.Application.Features.Projects;

public sealed record ProjectRequest(
    long DepartmentId,
    string Code,
    string Name,
    long? OwnerUserId = null,
    string? Description = null,
    string? Status = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    decimal? Budget = null);
