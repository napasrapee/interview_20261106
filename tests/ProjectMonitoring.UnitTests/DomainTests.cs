using ProjectMonitoring.Domain;

namespace ProjectMonitoring.UnitTests;

public class DomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 8, 0, 0, TimeSpan.Zero);

    private static WorkTask NewTask(WorkTaskStatus status, DateOnly? dueDate = null) =>
        new(1, null, "Task", null, status, TaskPriority.Medium, dueDate, Now);

    [Theory]
    [InlineData(ProjectStatus.OnHold, "on_hold")]
    [InlineData(ProjectStatus.Cancelled, "cancelled")]
    public void Enum_codes_round_trip(ProjectStatus status, string code)
    {
        Assert.Equal(code, status.ToCode());
        Assert.Equal(status, EnumCodes.Parse<ProjectStatus>(code));
        Assert.True(EnumCodes.IsValid<ProjectStatus>(code));
    }

    [Theory]
    [InlineData("OnHold")]
    [InlineData("unknown")]
    [InlineData(null)]
    public void Invalid_enum_codes_are_rejected(string? code) =>
        Assert.False(EnumCodes.IsValid<ProjectStatus>(code));

    [Fact]
    public void Parsing_invalid_enum_code_throws() =>
        Assert.Throws<DomainRuleException>(() => EnumCodes.Parse<WorkTaskStatus>("closed"));

    [Fact]
    public void Task_created_as_done_has_completed_at() =>
        Assert.Equal(Now, NewTask(WorkTaskStatus.Done).CompletedAt);

    [Fact]
    public void Completing_twice_keeps_first_completed_at()
    {
        var task = NewTask(WorkTaskStatus.Done);
        task.ChangeStatus(WorkTaskStatus.Done, Now.AddDays(1));
        Assert.Equal(Now, task.CompletedAt);
    }

    [Fact]
    public void Reopening_clears_completed_at()
    {
        var task = NewTask(WorkTaskStatus.Done);
        task.ChangeStatus(WorkTaskStatus.InProgress, Now);
        Assert.Null(task.CompletedAt);
        Assert.Equal(WorkTaskStatus.InProgress, task.Status);
    }

    [Fact]
    public void Overdue_requires_open_task_with_past_due_date()
    {
        var today = new DateOnly(2026, 11, 6);
        Assert.True(NewTask(WorkTaskStatus.Todo, today.AddDays(-1)).IsOverdue(today));
        Assert.False(NewTask(WorkTaskStatus.Todo, today).IsOverdue(today));
        Assert.False(NewTask(WorkTaskStatus.Todo).IsOverdue(today));
        Assert.False(NewTask(WorkTaskStatus.Done, today.AddDays(-1)).IsOverdue(today));
    }

    [Fact]
    public void Project_rejects_end_date_before_start_date() =>
        Assert.Throws<DomainRuleException>(() => new Project(
            1, null, "P", "P", null, ProjectStatus.Active,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), null));

    [Fact]
    public void Project_rejects_negative_budget() =>
        Assert.Throws<DomainRuleException>(() => new Project(
            1, null, "P", "P", null, ProjectStatus.Active, null, null, -1m));

    [Fact]
    public void Login_requires_active_account_with_password()
    {
        var user = new User(1, "A", "a@example.com", UserRole.Member, isActive: true);
        Assert.False(user.CanLogIn);

        user.SetPasswordHash("hash");
        Assert.True(user.CanLogIn);

        user.Update(1, "A", "a@example.com", UserRole.Member, isActive: false);
        Assert.False(user.CanLogIn);
    }
}
