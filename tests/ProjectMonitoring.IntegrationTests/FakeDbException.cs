namespace ProjectMonitoring.IntegrationTests;

public sealed class FakeDbException(bool isTransient) : System.Data.Common.DbException("secret database detail")
{
    public override bool IsTransient { get; } = isTransient;
}
