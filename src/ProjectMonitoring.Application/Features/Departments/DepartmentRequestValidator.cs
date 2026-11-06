using FluentValidation;

namespace ProjectMonitoring.Application.Features.Departments;

public sealed class DepartmentRequestValidator : AbstractValidator<DepartmentRequest>
{
    public DepartmentRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(20);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(150);
    }
}
