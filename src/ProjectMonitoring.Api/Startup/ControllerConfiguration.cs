using Microsoft.AspNetCore.Mvc;
using ProjectMonitoring.Api.Errors;
using ProjectMonitoring.Api.Json;

namespace ProjectMonitoring.Api.Startup;

public static class ControllerConfiguration
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new NullCharacterRejectingConverter()))
            .ConfigureApiBehaviorOptions(options =>
            {
                // Status-only results go through UseStatusCodePages, so all errors share one format.
                options.SuppressMapClientErrors = true;
                options.InvalidModelStateResponseFactory = CreateInvalidModelResponse;
            });
        return services;
    }

    private static ObjectResult CreateInvalidModelResponse(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

        // System.Text.Json reports unreadable bodies under keys that start with "$".
        var malformed = errors.Keys.Any(key => key.StartsWith('$')) || errors.ContainsKey("request");
        var problem = malformed
            ? Problems.Create(
                context.HttpContext, 400, ErrorCodes.MalformedRequest, "Malformed request",
                "The request body could not be read.")
            : Problems.Create(
                context.HttpContext, 400, ErrorCodes.ValidationError, "Validation failed",
                "One or more fields are invalid.", errors);
        return new ObjectResult(problem)
        {
            StatusCode = 400,
            ContentTypes = { "application/problem+json" },
        };
    }
}
