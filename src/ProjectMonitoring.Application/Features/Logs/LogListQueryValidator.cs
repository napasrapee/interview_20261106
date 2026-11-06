using FluentValidation;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features.Logs;

public sealed class LogListQueryValidator : AbstractValidator<LogListQuery>
{
    public LogListQueryValidator()
    {
        this.ApplyPageRules();
        RuleFor(query => query.Level)
            .Must(EnumCodes.IsValid<SystemLogLevel>)
            .WithMessage("Level must be info, warning or error.")
            .When(query => query.Level is not null);
        RuleFor(query => query.EventType)
            .MaximumLength(50)
            .Must(eventType => eventType is null || !eventType.Contains('\0'))
            .WithMessage("EventType must not contain null characters.");
        RuleFor(query => query.To)
            .GreaterThanOrEqualTo(query => query.From)
            .When(query => query.From is not null && query.To is not null);
    }
}
