using FluentValidation;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features.Tasks;

public sealed class TaskListQueryValidator : AbstractValidator<TaskListQuery>
{
    public TaskListQueryValidator()
    {
        this.ApplyPageRules();
        RuleFor(query => query.Status)
            .Must(EnumCodes.IsValid<WorkTaskStatus>)
            .WithMessage("Status must be todo, in_progress or done.")
            .When(query => query.Status is not null);
    }
}
