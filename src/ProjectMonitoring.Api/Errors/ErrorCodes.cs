namespace ProjectMonitoring.Api.Errors;

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
    public const string PayloadTooLarge = "payload_too_large";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string RateLimited = "rate_limited";
    public const string InternalError = "internal_error";
    public const string DatabaseUnavailable = "database_unavailable";
}
