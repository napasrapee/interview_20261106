using FluentValidation;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features.Tasks;

public sealed class TaskStatusRequestValidator : AbstractValidator<TaskStatusRequest>
{
    public TaskStatusRequestValidator() =>
        RuleFor(request => request.Status)
            .Must(EnumCodes.IsValid<WorkTaskStatus>)
            .WithMessage("Status must be todo, in_progress or done.");
}
