using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ProjectMonitoring.Api.Errors;

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
