using Microsoft.EntityFrameworkCore;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Users;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Infrastructure.Persistence;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<User?> FindAsync(long id, CancellationToken cancellationToken) =>
        await db.Users.FindAsync([id], cancellationToken);

    // ToLower translates to lower(email), which uses ux_users_email_lower.
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(user => user.Email.ToLower() == email.ToLower(), cancellationToken);

    public Task<PagedResult<User>> ListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsQueryable();
        if (query.DepartmentId is { } departmentId)
        {
            users = users.Where(user => user.DepartmentId == departmentId);
        }

        if (query.Role is not null)
        {
            var role = EnumCodes.Parse<UserRole>(query.Role);
            users = users.Where(user => user.Role == role);
        }

        return users.OrderBy(user => user.Id).ToPagedResultAsync(query, cancellationToken);
    }

    public Task<bool> HasUsableAdminAsync(CancellationToken cancellationToken) =>
        db.Users.AnyAsync(
            user => user.Role == UserRole.Admin && user.IsActive && user.PasswordHash != null, cancellationToken);

    public void Add(User user) => db.Users.Add(user);

    public void Remove(User user) => db.Users.Remove(user);
}
