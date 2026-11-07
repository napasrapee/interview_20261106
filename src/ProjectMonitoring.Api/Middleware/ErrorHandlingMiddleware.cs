using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ProjectMonitoring.Api.Errors;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Api.Middleware;

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
            HttpMethod = RequestLogContext.Method(context),
            Path = RequestLogContext.Path(context),
            StatusCode = problem.Status,
            TraceId = RequestLogContext.TraceId(context),
            UserId = RequestLogContext.UserId(context),
            Details = details,
        };
    }

    private static ProblemDetails Map(HttpContext context, Exception exception) => exception switch
    {
        // Kestrel raises this while reading a body that is too large or malformed.
        BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } => Problems.Create(
            context, 413, ErrorCodes.PayloadTooLarge, "Payload too large", "The request body is too large."),
        BadHttpRequestException => Problems.Create(
            context, 400, ErrorCodes.MalformedRequest, "Malformed request", "The request could not be read."),
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
        _ when IsDatabaseOutage(exception) => Problems.Create(
            context, 503, ErrorCodes.DatabaseUnavailable, "Service unavailable",
            "The database is not available. Try again later."),
        _ => Problems.Create(
            context, 500, ErrorCodes.InternalError, "Internal server error", "An unexpected error occurred."),
    };

    // EF Core may wrap the driver's exception. DbException keeps this layer free of Npgsql.
    private static bool IsDatabaseOutage(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException { IsTransient: true })
            {
                return true;
            }
        }

        return false;
    }
}
