using FluentValidation;

namespace ProjectMonitoring.Application.Features.Reports;

public sealed class ReportQueryValidator : AbstractValidator<ReportQuery>
{
    public ReportQueryValidator() =>
        RuleFor(query => query.DepartmentId).GreaterThan(0).When(query => query.DepartmentId is not null);
}
