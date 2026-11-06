using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features;
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
    public const string MethodNotAllowed = "method_not_allowed";
    public const string UnsupportedMediaType = "unsupported_media_type";
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

    /// <summary>Gives framework responses with an empty body the same format as other errors.</summary>
    public static Task WriteEmptyStatusAsync(HttpContext context) => context.Response.StatusCode switch
    {
        StatusCodes.Status404NotFound => WriteAsync(context, Create(
            context, 404, ErrorCodes.NotFound, "Not found", "The requested resource was not found.")),
        StatusCodes.Status405MethodNotAllowed => WriteAsync(context, Create(
            context, 405, ErrorCodes.MethodNotAllowed, "Method not allowed", "This method is not supported here.")),
        StatusCodes.Status415UnsupportedMediaType => WriteAsync(context, Create(
            context, 415, ErrorCodes.UnsupportedMediaType, "Unsupported media type", "Send the request body as JSON.")),
        _ => Task.CompletedTask,
    };

    public static Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}

public sealed class ErrorHandlingMiddleware(
    RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, ISystemLogWriter logWriter)
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

            await logWriter.WriteAsync(ToLog(context, exception, problem));

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            await Problems.WriteAsync(context, problem);
        }
    }

    private static SystemLog ToLog(HttpContext context, Exception exception, ProblemDetails problem)
    {
        var isServerError = problem.Status >= StatusCodes.Status500InternalServerError;
        var eventType = exception switch
        {
            InvalidCredentialsException => "login_failed",
            _ when isServerError => "exception",
            _ => "request_rejected",
        };

        // Rejections are expected, so only unexpected failures keep the exception details.
        // Login failures store no email, as people sometimes type a password into that field.
        var details = isServerError
            ? JsonSerializer.Serialize(new { exceptionType = exception.GetType().FullName, message = exception.Message })
            : null;

        return new SystemLog(
            isServerError ? SystemLogLevel.Error : SystemLogLevel.Warning,
            eventType,
            (string)problem.Extensions["code"]!)
        {
            HttpMethod = context.Request.Method,
            Path = RequestLoggingMiddleware.Truncate(context.Request.Path.Value, 500),
            StatusCode = problem.Status,
            TraceId = RequestLogContext.TraceId(context),
            UserId = RequestLogContext.UserId(context),
            Details = details,
        };
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
