using FluentValidation;

namespace ProjectMonitoring.Application.Common;

public static class ValidationExtensions
{
    // Keeps (Page - 1) * PageSize within int, which the database offset is computed as.
    public const int MaxPage = 1_000_000;

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
        validator.RuleFor(query => query.Page).InclusiveBetween(1, MaxPage);
        validator.RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
