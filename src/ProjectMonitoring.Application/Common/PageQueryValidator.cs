using FluentValidation;

namespace ProjectMonitoring.Application.Common;

public sealed class PageQueryValidator : AbstractValidator<PageQuery>
{
    public PageQueryValidator() => this.ApplyPageRules();
}
