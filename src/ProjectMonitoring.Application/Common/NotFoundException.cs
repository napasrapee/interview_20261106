namespace ProjectMonitoring.Application.Common;

public sealed class NotFoundException(string resource) : Exception($"{resource} was not found.")
{
    public NotFoundException(string resource, long id) : this($"{resource} {id}")
    {
    }
}
