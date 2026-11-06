using FluentValidation;

namespace ProjectMonitoring.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);

    public PagedResult<TResult> Map<TResult>(Func<T, TResult> selector) =>
        new(Items.Select(selector).ToList(), Page, PageSize, TotalItems);
}

public record PageQuery(int Page = 1, int PageSize = 20);

public sealed class NotFoundException(string resource, long id) : Exception($"{resource} {id} was not found.");

public sealed class ConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class ForbiddenException(string message) : Exception(message);

public sealed class InvalidCredentialsException() : Exception("Email or password is incorrect.");

public sealed class UnauthorizedException(string message) : Exception(message);

public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more fields are invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

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
