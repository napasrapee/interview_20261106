namespace ProjectMonitoring.Application.Common;

public sealed class ForbiddenException(string message) : Exception(message);
