using FluentValidation;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Users;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features.Auth;

/// <summary>Creates the first admin from configuration, so no credentials live in the seed data.</summary>
public sealed class AdminBootstrapper(
    IUserRepository users,
    IDepartmentRepository departments,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IValidator<UserRequest> validator)
{
    private const string UnsetValue = "change-me";

    public async Task EnsureAdminExistsAsync(AdminAccountOptions options, CancellationToken cancellationToken)
    {
        if (await users.FindByEmailAsync(options.Email, cancellationToken) is not null)
        {
            return;
        }

        if (options.Password == UnsetValue)
        {
            throw new InvalidOperationException("Set ADMIN_PASSWORD in .env before starting the API.");
        }

        var departmentId = await FindOrCreateDepartmentAsync(cancellationToken);
        var request = new UserRequest(departmentId, "System Administrator", options.Email, options.Password, "admin");
        await validator.EnsureValidAsync(request, cancellationToken);

        var admin = new User(departmentId, request.FullName, request.Email, UserRole.Admin, isActive: true);
        admin.SetPasswordHash(passwordHasher.Hash(options.Password));
        users.Add(admin);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<long> FindOrCreateDepartmentAsync(CancellationToken cancellationToken)
    {
        var existing = await departments.ListAsync(new PageQuery(1, 1), cancellationToken);
        if (existing.Items.Count > 0)
        {
            return existing.Items[0].Id;
        }

        var department = new Department("ADMIN", "ผู้ดูแลระบบ");
        departments.Add(department);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return department.Id;
    }
}
