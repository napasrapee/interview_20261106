namespace ProjectMonitoring.Application.Abstractions;

public interface IDatabaseHealthCheck
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
}
