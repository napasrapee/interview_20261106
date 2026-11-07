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
        if (await users.FindByEmailAsync(options.Email, cancellationToken) is null)
        {
            await CreateAdminAsync(options, cancellationToken);
        }

        // An existing account is left as it is. Without any admin who can log in, nobody could manage
        // the system, so refusing to start is safer than running.
        if (!await users.HasUsableAdminAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"No active admin with a password exists. ADMIN_EMAIL {options.Email} belongs to an existing " +
                "account that cannot log in as admin. Set ADMIN_EMAIL to an unused email.");
        }
    }

    private async Task CreateAdminAsync(AdminAccountOptions options, CancellationToken cancellationToken)
    {
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
