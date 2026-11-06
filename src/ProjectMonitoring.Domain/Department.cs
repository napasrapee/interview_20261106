namespace ProjectMonitoring.Domain;

public sealed class Department
{
    private Department()
    {
    }

    public Department(string code, string name) => Update(code, name);

    public long Id { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string code, string name)
    {
        Code = code;
        Name = name;
    }
}
