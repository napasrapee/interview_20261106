namespace ProjectMonitoring.Api.Security;

public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string ManagerOrAdmin = nameof(ManagerOrAdmin);
    public const string Login = nameof(Login);
}
