namespace ProjectMonitoring.Application.Features.Tasks;

public sealed record TaskRequest(
    long ProjectId,
    string Title,
    long? AssigneeUserId = null,
    string? Description = null,
    string? Status = null,
    string? Priority = null,
    DateOnly? DueDate = null);
