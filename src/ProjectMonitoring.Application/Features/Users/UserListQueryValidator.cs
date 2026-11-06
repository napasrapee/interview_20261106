using FluentValidation;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features.Users;

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
