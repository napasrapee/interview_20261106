using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Users;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindAsync(long id, CancellationToken cancellationToken);
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<PagedResult<User>> ListAsync(UserListQuery query, CancellationToken cancellationToken);
    Task<bool> HasUsableAdminAsync(CancellationToken cancellationToken);
    void Add(User user);
    void Remove(User user);
}
