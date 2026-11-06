using FluentValidation;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features.Projects;

public sealed class ProjectListQueryValidator : AbstractValidator<ProjectListQuery>
{
    public ProjectListQueryValidator()
    {
        this.ApplyPageRules();
        RuleFor(query => query.Status)
            .Must(EnumCodes.IsValid<ProjectStatus>)
            .WithMessage("Status must be planned, active, on_hold, completed or cancelled.")
            .When(query => query.Status is not null);
    }
}
