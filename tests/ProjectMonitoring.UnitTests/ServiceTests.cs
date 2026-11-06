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
        public int Verifications { get; private set; }

        public bool Verify(string password, string? passwordHash)
        {
            Verifications++;
            return passwordHash == "hashed:" + password;
        }
    }

    private sealed class FakeTokens : ITokenIssuer
    {
        public AccessToken Issue(User user) => new("token", 3600);
    }

    private sealed class FakeUsers(User? user = null) : IUserRepository
    {
        public User? Added { get; private set; }
        public bool Removed { get; private set; }
        public Task<User?> FindAsync(long id, CancellationToken cancellationToken) => Task.FromResult(user);
        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(user);
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
    public async Task Cannot_change_own_role()
    {
        var service = NewUserService(new FakeUsers(ExistingUser()), new FakeUnitOfWork(), currentUserId: 5);
        var request = new UserRequest(1, "A", "a@example.com", Role: "admin");
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
    public async Task Update_without_role_or_active_flag_keeps_access_rights()
    {
        var user = new User(1, "A", "a@example.com", UserRole.Manager, isActive: false);

        await NewUserService(new FakeUsers(user), new FakeUnitOfWork())
            .UpdateAsync(2, new UserRequest(1, "Renamed", "a@example.com"), None);

        Assert.Equal("Renamed", user.FullName);
        Assert.Equal(UserRole.Manager, user.Role);
        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task Own_account_can_be_renamed_when_role_is_omitted()
    {
        var user = new User(1, "A", "a@example.com", UserRole.Admin, isActive: true);

        var result = await NewUserService(new FakeUsers(user), new FakeUnitOfWork(), currentUserId: 5)
            .UpdateAsync(5, new UserRequest(1, "Renamed", "a@example.com"), None);

        Assert.Equal("admin", result.Role);
    }

    [Fact]
    public async Task Task_update_without_status_or_priority_keeps_them()
    {
        var completedAt = DateTimeOffset.UtcNow.AddDays(-2);
        var task = new WorkTask(1, null, "Task", null, WorkTaskStatus.Done, TaskPriority.High, null, completedAt);

        var result = await NewTaskService(new FakeTasks(task), new FakeUnitOfWork(), 7, UserRole.Manager)
            .UpdateAsync(1, new TaskRequest(1, "Renamed"), None);

        Assert.Equal("done", result.Status);
        Assert.Equal("high", result.Priority);
        Assert.Equal(completedAt, result.CompletedAt);
    }

    [Fact]
    public async Task Task_update_can_still_change_status_and_priority()
    {
        var task = new WorkTask(1, null, "Task", null, WorkTaskStatus.Done, TaskPriority.High, null, DateTimeOffset.UtcNow);

        var result = await NewTaskService(new FakeTasks(task), new FakeUnitOfWork(), 7, UserRole.Manager)
            .UpdateAsync(1, new TaskRequest(1, "Task", Status: "todo", Priority: "low"), None);

        Assert.Equal("todo", result.Status);
        Assert.Equal("low", result.Priority);
        Assert.Null(result.CompletedAt);
    }

    private static AuthService NewAuthService(FakeUsers users, FakeHasher hasher) =>
        new(users, hasher, new FakeTokens(), new FakeCurrentUser(null, null),
            NewUserService(users, new FakeUnitOfWork()), new LoginRequestValidator());

    private static readonly LoginRequest ValidLogin = new("a@example.com", "password1");

    [Fact]
    public async Task Correct_password_returns_bearer_token()
    {
        var user = ExistingUser();
        user.SetPasswordHash("hashed:password1");

        var result = await NewAuthService(new FakeUsers(user), new FakeHasher()).LoginAsync(ValidLogin, None);

        Assert.Equal("token", result.AccessToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(3600, result.ExpiresIn);
    }

    [Fact]
    public async Task Unknown_email_fails_after_running_password_check()
    {
        var hasher = new FakeHasher();
        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => NewAuthService(new FakeUsers(), hasher).LoginAsync(ValidLogin, None));
        Assert.Equal(1, hasher.Verifications);
    }

    [Fact]
    public async Task Wrong_password_fails()
    {
        var user = ExistingUser();
        user.SetPasswordHash("hashed:other");
        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => NewAuthService(new FakeUsers(user), new FakeHasher()).LoginAsync(ValidLogin, None));
    }

    [Fact]
    public async Task Inactive_account_fails_with_correct_password()
    {
        var user = new User(1, "A", "a@example.com", UserRole.Member, isActive: false);
        user.SetPasswordHash("hashed:password1");
        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => NewAuthService(new FakeUsers(user), new FakeHasher()).LoginAsync(ValidLogin, None));
    }

    [Fact]
    public async Task Account_without_password_fails()
    {
        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => NewAuthService(new FakeUsers(ExistingUser()), new FakeHasher()).LoginAsync(ValidLogin, None));
    }

    [Fact]
    public void Paged_result_rounds_total_pages_up()
    {
        Assert.Equal(3, new PagedResult<int>([], 1, 20, 41).TotalPages);
        Assert.Equal(0, new PagedResult<int>([], 1, 20, 0).TotalPages);
    }
}
