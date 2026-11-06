using Microsoft.EntityFrameworkCore;
using ProjectMonitoring.Application.Common;

namespace ProjectMonitoring.Infrastructure.Persistence;

internal static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PageQuery page, CancellationToken cancellationToken)
        where T : class
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }
}
