namespace ProjectMonitoring.Domain;

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
