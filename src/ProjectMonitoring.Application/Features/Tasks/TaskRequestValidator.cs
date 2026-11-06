using FluentValidation;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features.Tasks;

public sealed class TaskRequestValidator : AbstractValidator<TaskRequest>
{
    public TaskRequestValidator()
    {
        RuleFor(request => request.ProjectId).GreaterThan(0);
        RuleFor(request => request.AssigneeUserId).GreaterThan(0).When(request => request.AssigneeUserId is not null);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Status)
            .Must(EnumCodes.IsValid<WorkTaskStatus>)
            .WithMessage("Status must be todo, in_progress or done.")
            .When(request => request.Status is not null);
        RuleFor(request => request.Priority)
            .Must(EnumCodes.IsValid<TaskPriority>)
            .WithMessage("Priority must be low, medium or high.")
            .When(request => request.Priority is not null);
    }
}
