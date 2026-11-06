using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Abstractions;

public interface ITokenIssuer
{
    AccessToken Issue(User user);
}
