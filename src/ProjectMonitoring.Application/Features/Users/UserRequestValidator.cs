using System.Text;
using FluentValidation;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features.Users;

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
