using Microsoft.Extensions.DependencyInjection;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Application.Features.Auth;
using ProjectMonitoring.Application.Features.Logs;
using ProjectMonitoring.Application.Features.Projects;
using ProjectMonitoring.Application.Features.Tasks;
using ProjectMonitoring.Application.Features.Users;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.IntegrationTests;

public class PersistenceTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private IServiceScope Scope() => fixture.Services.CreateScope();

    private static async Task<Department> CreateDepartmentAsync(IServiceScope scope, string code)
    {
        var department = new Department(code, "Department " + code);
        scope.ServiceProvider.GetRequiredService<IDepartmentRepository>().Add(department);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(None);
        return department;
    }

    [Fact]
    public async Task Log_of_a_deleted_user_is_kept_without_the_link()
    {
        await fixture.Services.GetRequiredService<ISystemLogWriter>().WriteAsync(
            new SystemLog(SystemLogLevel.Info, "deleted_user_test", "message") { UserId = 999999 });

        using var scope = Scope();
        var logs = await scope.ServiceProvider.GetRequiredService<ISystemLogRepository>()
            .ListAsync(new LogListQuery(EventType: "deleted_user_test"), None);

        var log = Assert.Single(logs.Items);
        Assert.Null(log.UserId);
        Assert.Equal(999999, System.Text.Json.JsonDocument.Parse(log.Details!).RootElement.GetProperty("deletedUserId").GetInt64());
    }

    [Fact]
    public async Task Deleting_a_row_that_another_request_deleted_is_conflict()
    {
        long taskId;
        using (var setup = Scope())
        {
            var task = new WorkTask(1, null, "Race", null, WorkTaskStatus.Todo, TaskPriority.Low, null, DateTimeOffset.UtcNow);
            setup.ServiceProvider.GetRequiredService<IWorkTaskRepository>().Add(task);
            await setup.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(None);
            taskId = task.Id;
        }

        using var first = Scope();
        using var second = Scope();
        var firstTasks = first.ServiceProvider.GetRequiredService<IWorkTaskRepository>();
        var secondTasks = second.ServiceProvider.GetRequiredService<IWorkTaskRepository>();
        var firstCopy = (await firstTasks.FindAsync(taskId, None))!;
        var secondCopy = (await secondTasks.FindAsync(taskId, None))!;

        secondTasks.Remove(secondCopy);
        await second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(None);

        firstTasks.Remove(firstCopy);
        await Assert.ThrowsAsync<ConflictException>(
            () => first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(None));
    }

    [Fact]
    public async Task Admin_bootstrap_refuses_to_leave_the_system_without_an_admin()
    {
        using (var scope = Scope())
        {
            var bootstrapper = scope.ServiceProvider.GetRequiredService<AdminBootstrapper>();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrapper.EnsureAdminExistsAsync(
                new AdminAccountOptions("somchai@example.com", "bootstrap-password-1"), None));
            Assert.Contains("ADMIN_EMAIL", error.Message, StringComparison.Ordinal);
        }

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().EnsureAdminExistsAsync(
                new AdminAccountOptions("bootstrap@example.com", "bootstrap-password-1"), None);
            Assert.True(await scope.ServiceProvider.GetRequiredService<IUserRepository>().HasUsableAdminAsync(None));
        }

        // Once a usable admin exists, an existing non-admin email no longer blocks startup.
        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().EnsureAdminExistsAsync(
                new AdminAccountOptions("somchai@example.com", "bootstrap-password-1"), None);
        }
    }

    [Fact]
    public async Task Seed_is_read_with_enums_and_dates()
    {
        using var scope = Scope();
        var result = await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .ListAsync(new ProjectListQuery(Status: "cancelled"), None);

        var project = Assert.Single(result.Items);
        Assert.Equal("PRJ-FN-01", project.Code);
        Assert.Equal(ProjectStatus.Cancelled, project.Status);
        Assert.Equal(new DateOnly(2026, 6, 1), project.StartDate);
        Assert.Equal(500000m, project.Budget);
    }

    [Fact]
    public async Task Insert_returns_database_generated_values()
    {
        using var scope = Scope();
        var department = await CreateDepartmentAsync(scope, "INS");

        Assert.True(department.Id > 0);
        Assert.NotEqual(default, department.CreatedAt);
        Assert.NotEqual(default, department.UpdatedAt);
    }

    [Fact]
    public async Task Update_returns_trigger_updated_at()
    {
        using var scope = Scope();
        var department = await CreateDepartmentAsync(scope, "UPD");
        var before = department.UpdatedAt;

        department.Update("UPD", "Renamed");
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(None);

        Assert.True(department.UpdatedAt > before);
    }

    [Fact]
    public async Task Duplicate_code_is_conflict()
    {
        using var scope = Scope();
        scope.ServiceProvider.GetRequiredService<IDepartmentRepository>().Add(new Department("IT", "Another IT"));
        await Assert.ThrowsAsync<ConflictException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(None));
    }

    [Fact]
    public async Task Duplicate_email_with_other_case_is_conflict()
    {
        using var scope = Scope();
        scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .Add(new User(1, "Copy", "SOMCHAI@example.com", UserRole.Member, isActive: true));
        await Assert.ThrowsAsync<ConflictException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(None));
    }

    [Fact]
    public async Task Missing_foreign_key_is_conflict()
    {
        using var scope = Scope();
        scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .Add(new User(999999, "Nobody", "nobody@example.com", UserRole.Member, isActive: true));
        await Assert.ThrowsAsync<ConflictException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(None));
    }

    [Fact]
    public async Task Deleting_department_in_use_is_conflict()
    {
        using var scope = Scope();
        var departments = scope.ServiceProvider.GetRequiredService<IDepartmentRepository>();
        departments.Remove((await departments.FindAsync(1, None))!);
        await Assert.ThrowsAsync<ConflictException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(None));
    }

    [Fact]
    public async Task Deleting_project_cascades_to_tasks_and_task_lifecycle_persists()
    {
        long projectId;
        long taskId;
        using (var scope = Scope())
        {
            var project = new Project(4, null, "PRJ-CASCADE", "Cascade", null, ProjectStatus.Active, null, null, null);
            scope.ServiceProvider.GetRequiredService<IProjectRepository>().Add(project);
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.SaveChangesAsync(None);
            projectId = project.Id;

            var task = new WorkTask(projectId, null, "Task", null, WorkTaskStatus.Done, TaskPriority.High, Today, DateTimeOffset.UtcNow);
            scope.ServiceProvider.GetRequiredService<IWorkTaskRepository>().Add(task);
            await unitOfWork.SaveChangesAsync(None);
            taskId = task.Id;
        }

        using (var scope = Scope())
        {
            var tasks = scope.ServiceProvider.GetRequiredService<IWorkTaskRepository>();
            var task = await tasks.FindAsync(taskId, None);
            Assert.NotNull(task!.CompletedAt);

            // The database CHECK requires CompletedAt to be cleared together with the status.
            task.ChangeStatus(WorkTaskStatus.Todo, DateTimeOffset.UtcNow);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(None);
        }

        using (var scope = Scope())
        {
            var reopened = await scope.ServiceProvider.GetRequiredService<IWorkTaskRepository>().FindAsync(taskId, None);
            Assert.Null(reopened!.CompletedAt);

            var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            projects.Remove((await projects.FindAsync(projectId, None))!);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(None);
        }

        using (var scope = Scope())
        {
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IWorkTaskRepository>().FindAsync(taskId, None));
        }
    }

    [Fact]
    public async Task Task_filters_run_in_sql()
    {
        using var scope = Scope();
        var tasks = scope.ServiceProvider.GetRequiredService<IWorkTaskRepository>();

        var overdue = await tasks.ListAsync(new TaskListQuery(Overdue: true), Today, None);
        Assert.Equal(2, overdue.TotalItems);
        Assert.All(overdue.Items, task => Assert.True(task.IsOverdue(Today)));

        var notOverdue = await tasks.ListAsync(new TaskListQuery(Overdue: false, ProjectId: 1), Today, None);
        Assert.Equal(5, notOverdue.TotalItems);

        var inProgress = await tasks.ListAsync(new TaskListQuery(ProjectId: 1, Status: "in_progress"), Today, None);
        Assert.Equal(2, inProgress.TotalItems);
    }

    [Fact]
    public async Task Paging_returns_requested_slice()
    {
        using var scope = Scope();
        var tasks = scope.ServiceProvider.GetRequiredService<IWorkTaskRepository>();

        var page = await tasks.ListAsync(new TaskListQuery(ProjectId: 1, Page: 2, PageSize: 4), Today, None);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(6, page.TotalItems);
        Assert.Equal(2, page.TotalPages);
    }

    [Fact]
    public async Task User_filters_and_nullable_password_are_mapped()
    {
        using var scope = Scope();
        var result = await scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .ListAsync(new UserListQuery(DepartmentId: 1, Role: "manager"), None);

        var user = Assert.Single(result.Items);
        Assert.Equal("somchai@example.com", user.Email);
        Assert.Null(user.PasswordHash);
        Assert.False(user.CanLogIn);
    }

    [Fact]
    public async Task Health_check_reports_database_up()
    {
        var health = fixture.Services.GetRequiredService<IDatabaseHealthCheck>();
        Assert.True(await health.IsHealthyAsync(None));
    }
}
