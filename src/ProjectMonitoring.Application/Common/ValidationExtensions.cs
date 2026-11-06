using FluentValidation;

namespace ProjectMonitoring.Application.Common;

public static class ValidationExtensions
{
    public static async Task EnsureValidAsync<T>(
        this IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>(result.ToDictionary()));
        }
    }

    public static void ApplyPageRules<T>(this AbstractValidator<T> validator) where T : PageQuery
    {
        validator.RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        validator.RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
