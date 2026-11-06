using FluentValidation;

namespace ProjectMonitoring.Application.Features.Reports;

public sealed class ExportQueryValidator : AbstractValidator<ExportQuery>
{
    public ExportQueryValidator()
    {
        RuleFor(query => query.Format)
            .Must(format => format is "json" or "csv")
            .WithMessage("Format must be json or csv.");
        RuleFor(query => query.DepartmentId).GreaterThan(0).When(query => query.DepartmentId is not null);
    }
}
