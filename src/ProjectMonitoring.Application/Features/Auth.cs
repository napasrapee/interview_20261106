using FluentValidation;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Password).NotEmpty();
    }
}

public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    ICurrentUser currentUser,
    UserService userService,
    IValidator<LoginRequest> validator)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);
        var user = await users.FindByEmailAsync(request.Email, cancellationToken);

        // Verify first, so unknown and known accounts take the same time.
        var passwordMatches = passwordHasher.Verify(request.Password, user?.PasswordHash);
        if (user is null || !passwordMatches || !user.CanLogIn)
        {
            throw new InvalidCredentialsException();
        }

        var token = tokenIssuer.Issue(user);
        return new LoginResponse(token.Value, "Bearer", token.ExpiresInSeconds);
    }

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } id)
        {
            throw new UnauthorizedException("Authentication is required.");
        }

        try
        {
            return await userService.GetAsync(id, cancellationToken);
        }
        catch (NotFoundException)
        {
            // The token is valid, but its account has been deleted.
            throw new UnauthorizedException("Authentication is required.");
        }
    }
}

public sealed record AdminAccountOptions(string Email, string Password);

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
