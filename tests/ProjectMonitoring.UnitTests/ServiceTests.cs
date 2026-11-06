using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.UnitTests;

public class ServiceTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private sealed class FakeCurrentUser(long? userId, UserRole? role) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public UserRole? Role { get; } = role;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;
        public bool Verify(string password, string passwordHash) => passwordHash == "hashed:" + password;
    }

    private sealed class FakeUsers(User? user = null) : IUserRepository
    {
        public User? Added { get; private set; }
        public bool Removed { get; private set; }
        public Task<User?> FindAsync(long id, CancellationToken cancellationToken) => Task.FromResult(user);
        public Task<PagedResult<User>> ListAsync(UserListQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public void Add(User newUser) => Added = newUser;
        public void Remove(User removedUser) => Removed = true;
    }

    private sealed class FakeTasks(WorkTask? task) : IWorkTaskRepository
    {
        public Task<WorkTask?> FindAsync(long id, CancellationToken cancellationToken) => Task.FromResult(task);
        public Task<PagedResult<WorkTask>> ListAsync(TaskListQuery query, DateOnly today, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public void Add(WorkTask newTask) => throw new NotSupportedException();
        public void Remove(WorkTask removedTask) => throw new NotSupportedException();
    }

    private static UserService NewUserService(FakeUsers users, FakeUnitOfWork unitOfWork, long currentUserId = 1) =>
        new(users, unitOfWork, new FakeHasher(), new FakeCurrentUser(currentUserId, UserRole.Admin),
            new UserRequestValidator(), new UserListQueryValidator());

    private static TaskService NewTaskService(FakeTasks tasks, FakeUnitOfWork unitOfWork, long userId, UserRole role) =>
        new(tasks, unitOfWork, new FakeCurrentUser(userId, role), TimeProvider.System,
            new TaskRequestValidator(), new TaskStatusRequestValidator(), new TaskListQueryValidator());

    private static WorkTask AssignedTo(long? assignee) =>
        new(1, assignee, "Task", null, WorkTaskStatus.Todo, TaskPriority.Medium, null, DateTimeOffset.UtcNow);

    private static User ExistingUser() => new(1, "A", "a@example.com", UserRole.Member, isActive: true);

    [Fact]
    public async Task Member_can_update_own_task()
    {
        var unitOfWork = new FakeUnitOfWork();
        var service = NewTaskService(new FakeTasks(AssignedTo(7)), unitOfWork, 7, UserRole.Member);

        var result = await service.ChangeStatusAsync(1, new TaskStatusRequest("done"), None);

        Assert.Equal("done", result.Status);
        Assert.NotNull(result.CompletedAt);
        Assert.Equal(1, unitOfWork.Saves);
    }

    [Theory]
    [InlineData(8L)]
    [InlineData(null)]
    public async Task Member_cannot_update_other_task(long? assignee)
    {
        var unitOfWork = new FakeUnitOfWork();
        var service = NewTaskService(new FakeTasks(AssignedTo(assignee)), unitOfWork, 7, UserRole.Member);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.ChangeStatusAsync(1, new TaskStatusRequest("done"), None));
        Assert.Equal(0, unitOfWork.Saves);
    }

    [Fact]
    public async Task Manager_can_update_any_task()
    {
        var service = NewTaskService(new FakeTasks(AssignedTo(8)), new FakeUnitOfWork(), 7, UserRole.Manager);
        Assert.Equal("in_progress", (await service.ChangeStatusAsync(1, new TaskStatusRequest("in_progress"), None)).Status);
    }

    [Fact]
    public async Task Missing_task_throws_not_found()
    {
        var service = NewTaskService(new FakeTasks(null), new FakeUnitOfWork(), 7, UserRole.Admin);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(1, None));
    }

    [Fact]
    public async Task Invalid_request_is_rejected_before_saving()
    {
        var unitOfWork = new FakeUnitOfWork();
        var service = NewTaskService(new FakeTasks(AssignedTo(7)), unitOfWork, 7, UserRole.Admin);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => service.ChangeStatusAsync(1, new TaskStatusRequest("closed"), None));

        Assert.True(exception.Errors.ContainsKey("Status"));
        Assert.Equal(0, unitOfWork.Saves);
    }

    [Fact]
    public async Task Cannot_delete_own_account()
    {
        var users = new FakeUsers(ExistingUser());
        await Assert.ThrowsAsync<ConflictException>(
            () => NewUserService(users, new FakeUnitOfWork(), currentUserId: 5).DeleteAsync(5, None));
        Assert.False(users.Removed);
    }

    [Fact]
    public async Task Cannot_deactivate_own_account()
    {
        var service = NewUserService(new FakeUsers(ExistingUser()), new FakeUnitOfWork(), currentUserId: 5);
        var request = new UserRequest(1, "A", "a@example.com", IsActive: false);
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(5, request, None));
    }

    [Fact]
    public async Task Can_delete_other_account()
    {
        var users = new FakeUsers(ExistingUser());
        await NewUserService(users, new FakeUnitOfWork(), currentUserId: 5).DeleteAsync(6, None);
        Assert.True(users.Removed);
    }

    [Fact]
    public async Task Create_hashes_password_and_dto_has_defaults()
    {
        var users = new FakeUsers();
        var result = await NewUserService(users, new FakeUnitOfWork())
            .CreateAsync(new UserRequest(1, "A", "a@example.com", "password1"), None);

        Assert.Equal("hashed:password1", users.Added!.PasswordHash);
        Assert.Equal("member", result.Role);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Update_without_password_keeps_existing_hash()
    {
        var user = ExistingUser();
        user.SetPasswordHash("original");

        await NewUserService(new FakeUsers(user), new FakeUnitOfWork())
            .UpdateAsync(2, new UserRequest(1, "B", "b@example.com"), None);

        Assert.Equal("original", user.PasswordHash);
        Assert.Equal("B", user.FullName);
    }

    [Fact]
    public void Paged_result_rounds_total_pages_up()
    {
        Assert.Equal(3, new PagedResult<int>([], 1, 20, 41).TotalPages);
        Assert.Equal(0, new PagedResult<int>([], 1, 20, 0).TotalPages);
    }
}
