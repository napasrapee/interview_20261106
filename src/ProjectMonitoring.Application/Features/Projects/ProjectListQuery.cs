using ProjectMonitoring.Application.Common;

namespace ProjectMonitoring.Application.Features.Projects;

public sealed record ProjectListQuery(long? DepartmentId = null, string? Status = null, int Page = 1, int PageSize = 20)
    : PageQuery(Page, PageSize);
