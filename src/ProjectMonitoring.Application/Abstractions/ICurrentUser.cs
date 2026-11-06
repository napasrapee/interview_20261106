using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Abstractions;

public interface ICurrentUser
{
    long? UserId { get; }
    UserRole? Role { get; }
}
