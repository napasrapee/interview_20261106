using ProjectMonitoring.Application.Common;

namespace ProjectMonitoring.Application.Features.Users;

public sealed record UserListQuery(long? DepartmentId = null, string? Role = null, int Page = 1, int PageSize = 20)
    : PageQuery(Page, PageSize);
