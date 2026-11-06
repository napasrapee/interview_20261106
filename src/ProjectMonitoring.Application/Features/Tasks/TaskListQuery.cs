using ProjectMonitoring.Application.Common;

namespace ProjectMonitoring.Application.Features.Tasks;

public sealed record TaskListQuery(
    long? ProjectId = null,
    long? AssigneeUserId = null,
    string? Status = null,
    bool? Overdue = null,
    int Page = 1,
    int PageSize = 20) : PageQuery(Page, PageSize);
