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

public sealed class User
{
    private User()
    {
    }

    public User(long departmentId, string fullName, string email, UserRole role, bool isActive) =>
        Update(departmentId, fullName, email, role, isActive);

    public long Id { get; private set; }
    public long DepartmentId { get; private set; }
    public string FullName { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string? PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>RULE-09: accounts that are inactive or have no password cannot log in.</summary>
    public bool CanLogIn => IsActive && PasswordHash is not null;

    public void Update(long departmentId, string fullName, string email, UserRole role, bool isActive)
    {
        DepartmentId = departmentId;
        FullName = fullName;
        Email = email;
        Role = role;
        IsActive = isActive;
    }

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;
}

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

public sealed class WorkTask
{
    private WorkTask()
    {
    }

    public WorkTask(
        long projectId,
        long? assigneeUserId,
        string title,
        string? description,
        WorkTaskStatus status,
        TaskPriority priority,
        DateOnly? dueDate,
        DateTimeOffset now) =>
        Update(projectId, assigneeUserId, title, description, status, priority, dueDate, now);

    public long Id { get; private set; }
    public long ProjectId { get; private set; }
    public long? AssigneeUserId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public WorkTaskStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        long projectId,
        long? assigneeUserId,
        string title,
        string? description,
        WorkTaskStatus status,
        TaskPriority priority,
        DateOnly? dueDate,
        DateTimeOffset now)
    {
        ProjectId = projectId;
        AssigneeUserId = assigneeUserId;
        Title = title;
        Description = description;
        Priority = priority;
        DueDate = dueDate;
        ChangeStatus(status, now);
    }

    /// <summary>RULE-05: CompletedAt is set on completion and cleared when reopened.</summary>
    public void ChangeStatus(WorkTaskStatus status, DateTimeOffset now)
    {
        if (status == WorkTaskStatus.Done && CompletedAt is null)
        {
            CompletedAt = now;
        }
        else if (status != WorkTaskStatus.Done)
        {
            CompletedAt = null;
        }

        Status = status;
    }

    /// <summary>RULE-04.</summary>
    public bool IsOverdue(DateOnly today) =>
        Status != WorkTaskStatus.Done && DueDate is not null && DueDate < today;
}

public sealed class SystemLog
{
    private SystemLog()
    {
    }

    public SystemLog(SystemLogLevel level, string eventType, string message)
    {
        Level = level;
        EventType = eventType;
        Message = message;
    }

    public long Id { get; private set; }
    public SystemLogLevel Level { get; private set; }
    public string EventType { get; private set; } = "";
    public string Message { get; private set; } = "";
    public string? HttpMethod { get; init; }
    public string? Path { get; init; }
    public int? StatusCode { get; init; }
    public int? DurationMs { get; init; }
    public string? TraceId { get; init; }
    public long? UserId { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset CreatedAt { get; private set; }
}
