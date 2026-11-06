namespace ProjectMonitoring.Domain;

public sealed class Project
{
    private Project()
    {
    }

    public Project(
        long departmentId,
        long? ownerUserId,
        string code,
        string name,
        string? description,
        ProjectStatus status,
        DateOnly? startDate,
        DateOnly? endDate,
        decimal? budget) =>
        Update(departmentId, ownerUserId, code, name, description, status, startDate, endDate, budget);

    public long Id { get; private set; }
    public long DepartmentId { get; private set; }
    public long? OwnerUserId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public ProjectStatus Status { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public decimal? Budget { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        long departmentId,
        long? ownerUserId,
        string code,
        string name,
        string? description,
        ProjectStatus status,
        DateOnly? startDate,
        DateOnly? endDate,
        decimal? budget)
    {
        // RULE-03
        if (startDate is not null && endDate is not null && endDate < startDate)
        {
            throw new DomainRuleException("End date must not be before start date.");
        }

        if (budget < 0)
        {
            throw new DomainRuleException("Budget must not be negative.");
        }

        DepartmentId = departmentId;
        OwnerUserId = ownerUserId;
        Code = code;
        Name = name;
        Description = description;
        Status = status;
        StartDate = startDate;
        EndDate = endDate;
        Budget = budget;
    }
}
