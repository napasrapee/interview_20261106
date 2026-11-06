using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Infrastructure.Persistence;

/// <summary>Maps entities onto the schema in db/init. The schema itself is not managed by EF.</summary>
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("departments");
            MapId(entity, department => department.Id);
            entity.Property(department => department.Code).HasColumnName("code");
            entity.Property(department => department.Name).HasColumnName("name");
            MapTimestamps(entity, department => department.CreatedAt, department => department.UpdatedAt);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            MapId(entity, user => user.Id);
            entity.Ignore(user => user.CanLogIn);
            entity.Property(user => user.DepartmentId).HasColumnName("department_id");
            entity.Property(user => user.FullName).HasColumnName("full_name");
            entity.Property(user => user.Email).HasColumnName("email");
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash");
            entity.Property(user => user.Role).HasColumnName("role").HasConversion(CodeConverter<UserRole>());
            entity.Property(user => user.IsActive).HasColumnName("is_active");
            MapTimestamps(entity, user => user.CreatedAt, user => user.UpdatedAt);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            MapId(entity, project => project.Id);
            entity.Property(project => project.DepartmentId).HasColumnName("department_id");
            entity.Property(project => project.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(project => project.Code).HasColumnName("code");
            entity.Property(project => project.Name).HasColumnName("name");
            entity.Property(project => project.Description).HasColumnName("description");
            entity.Property(project => project.Status).HasColumnName("status").HasConversion(CodeConverter<ProjectStatus>());
            entity.Property(project => project.StartDate).HasColumnName("start_date");
            entity.Property(project => project.EndDate).HasColumnName("end_date");
            entity.Property(project => project.Budget).HasColumnName("budget").HasPrecision(14, 2);
            MapTimestamps(entity, project => project.CreatedAt, project => project.UpdatedAt);
        });

        modelBuilder.Entity<WorkTask>(entity =>
        {
            entity.ToTable("tasks");
            MapId(entity, task => task.Id);
            entity.Property(task => task.ProjectId).HasColumnName("project_id");
            entity.Property(task => task.AssigneeUserId).HasColumnName("assignee_user_id");
            entity.Property(task => task.Title).HasColumnName("title");
            entity.Property(task => task.Description).HasColumnName("description");
            entity.Property(task => task.Status).HasColumnName("status").HasConversion(CodeConverter<WorkTaskStatus>());
            entity.Property(task => task.Priority).HasColumnName("priority").HasConversion(CodeConverter<TaskPriority>());
            entity.Property(task => task.DueDate).HasColumnName("due_date");
            entity.Property(task => task.CompletedAt).HasColumnName("completed_at");
            MapTimestamps(entity, task => task.CreatedAt, task => task.UpdatedAt);
        });

        modelBuilder.Entity<SystemLog>(entity =>
        {
            entity.ToTable("system_logs");
            MapId(entity, log => log.Id);
            entity.Property(log => log.Level).HasColumnName("level").HasConversion(CodeConverter<SystemLogLevel>());
            entity.Property(log => log.EventType).HasColumnName("event_type");
            entity.Property(log => log.Message).HasColumnName("message");
            entity.Property(log => log.HttpMethod).HasColumnName("http_method");
            entity.Property(log => log.Path).HasColumnName("path");
            entity.Property(log => log.StatusCode).HasColumnName("status_code");
            entity.Property(log => log.DurationMs).HasColumnName("duration_ms");
            entity.Property(log => log.TraceId).HasColumnName("trace_id");
            entity.Property(log => log.UserId).HasColumnName("user_id");
            entity.Property(log => log.Details).HasColumnName("details").HasColumnType("jsonb");
            entity.Property(log => log.CreatedAt).HasColumnName("created_at").ValueGeneratedOnAdd();
        });
    }

    private static void MapId<T>(EntityTypeBuilder<T> entity, Expression<Func<T, long>> id)
        where T : class
    {
        entity.HasKey(ToObjectExpression(id));
        entity.Property(id).HasColumnName("id").UseIdentityAlwaysColumn();
    }

    private static Expression<Func<T, object?>> ToObjectExpression<T>(Expression<Func<T, long>> id) =>
        Expression.Lambda<Func<T, object?>>(Expression.Convert(id.Body, typeof(object)), id.Parameters);

    private static void MapTimestamps<T>(
        EntityTypeBuilder<T> entity,
        Expression<Func<T, DateTimeOffset>> createdAt,
        Expression<Func<T, DateTimeOffset>> updatedAt)
        where T : class
    {
        // Set by column defaults and the set_updated_at trigger, then read back on save.
        entity.Property(createdAt).HasColumnName("created_at").ValueGeneratedOnAdd();
        entity.Property(updatedAt).HasColumnName("updated_at").ValueGeneratedOnAddOrUpdate();
    }

    private static ValueConverter<TEnum, string> CodeConverter<TEnum>() where TEnum : struct, Enum =>
        new(value => value.ToCode(), code => EnumCodes.Parse<TEnum>(code));
}
