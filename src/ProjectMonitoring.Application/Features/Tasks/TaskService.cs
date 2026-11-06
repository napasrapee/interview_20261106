using FluentValidation;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features.Tasks;

public sealed class TaskService(
    IWorkTaskRepository tasks,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<TaskRequest> validator,
    IValidator<TaskStatusRequest> statusValidator,
    IValidator<TaskListQuery> queryValidator)
{
    public async Task<PagedResult<TaskDto>> ListAsync(TaskListQuery query, CancellationToken cancellationToken)
    {
        await queryValidator.EnsureValidAsync(query, cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return (await tasks.ListAsync(query, today, cancellationToken)).Map(ToDto);
    }

    public async Task<TaskDto> GetAsync(long id, CancellationToken cancellationToken) =>
        ToDto(await RequireAsync(id, cancellationToken));

    public async Task<TaskDto> CreateAsync(TaskRequest request, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);
        var task = new WorkTask(
            request.ProjectId,
            request.AssigneeUserId,
            request.Title,
            request.Description,
            EnumCodes.ParseOr(request.Status, WorkTaskStatus.Todo),
            EnumCodes.ParseOr(request.Priority, TaskPriority.Medium),
            request.DueDate,
            timeProvider.GetUtcNow());
        tasks.Add(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(task);
    }

    public async Task<TaskDto> UpdateAsync(long id, TaskRequest request, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);
        var task = await RequireAsync(id, cancellationToken);
        task.Update(
            request.ProjectId,
            request.AssigneeUserId,
            request.Title,
            request.Description,
            EnumCodes.ParseOr(request.Status, task.Status),
            EnumCodes.ParseOr(request.Priority, task.Priority),
            request.DueDate,
            timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(task);
    }

    public async Task<TaskDto> ChangeStatusAsync(long id, TaskStatusRequest request, CancellationToken cancellationToken)
    {
        await statusValidator.EnsureValidAsync(request, cancellationToken);
        var task = await RequireAsync(id, cancellationToken);

        // RULE-10
        if (currentUser.Role == UserRole.Member && task.AssigneeUserId != currentUser.UserId)
        {
            throw new ForbiddenException("Members can only update tasks assigned to them.");
        }

        task.ChangeStatus(EnumCodes.Parse<WorkTaskStatus>(request.Status), timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(task);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        tasks.Remove(await RequireAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<WorkTask> RequireAsync(long id, CancellationToken cancellationToken) =>
        await tasks.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Task", id);

    private static TaskDto ToDto(WorkTask task) => new(
        task.Id,
        task.ProjectId,
        task.AssigneeUserId,
        task.Title,
        task.Description,
        task.Status.ToCode(),
        task.Priority.ToCode(),
        task.DueDate,
        task.CompletedAt,
        task.CreatedAt,
        task.UpdatedAt);
}
