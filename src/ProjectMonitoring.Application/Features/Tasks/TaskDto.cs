namespace ProjectMonitoring.Application.Features.Tasks;

public sealed record TaskDto(
    long Id,
    long ProjectId,
    long? AssigneeUserId,
    string Title,
    string? Description,
    string Status,
    string Priority,
    DateOnly? DueDate,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
