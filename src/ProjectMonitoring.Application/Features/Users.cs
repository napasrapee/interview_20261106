using System.Text;
using FluentValidation;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features;

public sealed record UserRequest(
    long DepartmentId,
    string FullName,
    string Email,
    string? Password = null,
    string? Role = null,
    bool? IsActive = null);

public sealed record UserDto(
    long Id,
    long DepartmentId,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UserListQuery(long? DepartmentId = null, string? Role = null, int Page = 1, int PageSize = 20)
    : PageQuery(Page, PageSize);

public sealed class UserRequestValidator : AbstractValidator<UserRequest>
{
    // BCrypt only uses the first 72 bytes, and Thai characters take 3 bytes each in UTF-8.
    private const int MaxPasswordBytes = 72;

    public UserRequestValidator()
    {
        RuleFor(request => request.DepartmentId).GreaterThan(0);
        RuleFor(request => request.FullName).NotEmpty().MaximumLength(150);
        RuleFor(request => request.Email).NotEmpty().MaximumLength(255).EmailAddress();
        RuleFor(request => request.Password!)
            .MinimumLength(8)
            .Must(password => Encoding.UTF8.GetByteCount(password) <= MaxPasswordBytes)
            .WithMessage($"Password must not exceed {MaxPasswordBytes} bytes.")
            .When(request => request.Password is not null);
        RuleFor(request => request.Role)
            .Must(EnumCodes.IsValid<UserRole>)
            .WithMessage("Role must be admin, manager or member.")
            .When(request => request.Role is not null);
    }
}

public sealed class UserListQueryValidator : AbstractValidator<UserListQuery>
{
    public UserListQueryValidator()
    {
        this.ApplyPageRules();
        RuleFor(query => query.Role)
            .Must(EnumCodes.IsValid<UserRole>)
            .WithMessage("Role must be admin, manager or member.")
            .When(query => query.Role is not null);
    }
}

public sealed class UserService(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IValidator<UserRequest> validator,
    IValidator<UserListQuery> queryValidator)
{
    public async Task<PagedResult<UserDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        await queryValidator.EnsureValidAsync(query, cancellationToken);
        return (await users.ListAsync(query, cancellationToken)).Map(ToDto);
    }

    public async Task<UserDto> GetAsync(long id, CancellationToken cancellationToken) =>
        ToDto(await RequireAsync(id, cancellationToken));

    public async Task<UserDto> CreateAsync(UserRequest request, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);
        var user = new User(
            request.DepartmentId,
            request.FullName,
            request.Email,
            EnumCodes.ParseOr(request.Role, UserRole.Member),
            request.IsActive ?? true);
        ApplyPassword(user, request.Password);
        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(long id, UserRequest request, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);
        var user = await RequireAsync(id, cancellationToken);
        // Omitted fields keep their value: renaming a user must not change access rights.
        var isActive = request.IsActive ?? user.IsActive;
        var role = EnumCodes.ParseOr(request.Role, user.Role);

        // RULE-08: stops the only admin from locking everyone out.
        if (id == currentUser.UserId && !isActive)
        {
            throw new ConflictException("You cannot deactivate your own account.");
        }

        if (id == currentUser.UserId && role != user.Role)
        {
            throw new ConflictException("You cannot change your own role.");
        }

        user.Update(request.DepartmentId, request.FullName, request.Email, role, isActive);
        ApplyPassword(user, request.Password);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        // RULE-08
        if (id == currentUser.UserId)
        {
            throw new ConflictException("You cannot delete your own account.");
        }

        users.Remove(await RequireAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void ApplyPassword(User user, string? password)
    {
        if (password is not null)
        {
            user.SetPasswordHash(passwordHasher.Hash(password));
        }
    }

    private async Task<User> RequireAsync(long id, CancellationToken cancellationToken) =>
        await users.FindAsync(id, cancellationToken) ?? throw new NotFoundException("User", id);

    private static UserDto ToDto(User user) => new(
        user.Id,
        user.DepartmentId,
        user.FullName,
        user.Email,
        user.Role.ToCode(),
        user.IsActive,
        user.CreatedAt,
        user.UpdatedAt);
}
