using ProjectMonitoring.Application.Common;

namespace ProjectMonitoring.Application.Features.Logs;

public sealed record LogListQuery(
    string? Level = null,
    string? EventType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20) : PageQuery(Page, PageSize);
