using FluentValidation;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features.Projects;

public sealed class ProjectRequestValidator : AbstractValidator<ProjectRequest>
{
    public ProjectRequestValidator()
    {
        RuleFor(request => request.DepartmentId).GreaterThan(0);
        RuleFor(request => request.OwnerUserId).GreaterThan(0).When(request => request.OwnerUserId is not null);
        RuleFor(request => request.Code).NotEmpty().MaximumLength(30);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Status)
            .Must(EnumCodes.IsValid<ProjectStatus>)
            .WithMessage("Status must be planned, active, on_hold, completed or cancelled.")
            .When(request => request.Status is not null);
        RuleFor(request => request.EndDate)
            .GreaterThanOrEqualTo(request => request.StartDate)
            .When(request => request.StartDate is not null && request.EndDate is not null);
        RuleFor(request => request.Budget)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(14, 2, ignoreTrailingZeros: true)
            .When(request => request.Budget is not null);
    }
}
