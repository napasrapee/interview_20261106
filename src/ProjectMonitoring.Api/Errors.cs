using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Api;

public static class ErrorCodes
{
    public const string ValidationError = "validation_error";
    public const string MalformedRequest = "malformed_request";
    public const string Unauthorized = "unauthorized";
    public const string InvalidCredentials = "invalid_credentials";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string RateLimited = "rate_limited";
    public const string InternalError = "internal_error";
}

/// <summary>Builds the single error format of the API: RFC 9457 problem+json.</summary>
public static class Problems
{
    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string code,
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails { Type = "about:blank", Title = title, Status = status, Detail = detail };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors.ToDictionary(
                error => JsonNamingPolicy.CamelCase.ConvertName(error.Key),
                error => error.Value);
        }

        return problem;
    }

    public static Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}

public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away; there is nobody to answer.
        }
        catch (Exception exception)
        {
            var problem = Map(context, exception);
            if (problem.Status >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Unhandled exception");
            }
            else
            {
                logger.LogWarning("Request failed: {Code}", problem.Extensions["code"]);
            }

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            await Problems.WriteAsync(context, problem);
        }
    }

    private static ProblemDetails Map(HttpContext context, Exception exception) => exception switch
    {
        RequestValidationException validation => Problems.Create(
            context, 400, ErrorCodes.ValidationError, "Validation failed", validation.Message, validation.Errors),
        DomainRuleException => Problems.Create(
            context, 400, ErrorCodes.ValidationError, "Validation failed", exception.Message),
        InvalidCredentialsException => Problems.Create(
            context, 401, ErrorCodes.InvalidCredentials, "Login failed", exception.Message),
        UnauthorizedException => Problems.Create(
            context, 401, ErrorCodes.Unauthorized, "Unauthorized", exception.Message),
        ForbiddenException => Problems.Create(
            context, 403, ErrorCodes.Forbidden, "Forbidden", exception.Message),
        NotFoundException => Problems.Create(
            context, 404, ErrorCodes.NotFound, "Not found", exception.Message),
        ConflictException => Problems.Create(
            context, 409, ErrorCodes.Conflict, "Conflict", exception.Message),
        _ => Problems.Create(
            context, 500, ErrorCodes.InternalError, "Internal server error", "An unexpected error occurred."),
    };
}
