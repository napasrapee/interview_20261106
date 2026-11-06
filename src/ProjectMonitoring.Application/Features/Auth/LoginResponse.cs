namespace ProjectMonitoring.Application.Features.Auth;

public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn);
