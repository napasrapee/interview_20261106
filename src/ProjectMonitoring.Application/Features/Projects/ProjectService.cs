using FluentValidation;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features.Projects;

public sealed class ProjectService(
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    IValidator<ProjectRequest> validator,
    IValidator<ProjectListQuery> queryValidator)
{
    public async Task<PagedResult<ProjectDto>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken)
    {
        await queryValidator.EnsureValidAsync(query, cancellationToken);
        return (await projects.ListAsync(query, cancellationToken)).Map(ToDto);
    }

    public async Task<ProjectDto> GetAsync(long id, CancellationToken cancellationToken) =>
        ToDto(await RequireAsync(id, cancellationToken));

    public async Task<ProjectDto> CreateAsync(ProjectRequest request, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);
        var project = new Project(
            request.DepartmentId,
            request.OwnerUserId,
            request.Code,
            request.Name,
            request.Description,
            EnumCodes.ParseOr(request.Status, ProjectStatus.Planned),
            request.StartDate,
            request.EndDate,
            request.Budget);
        projects.Add(project);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(project);
    }

    public async Task<ProjectDto> UpdateAsync(long id, ProjectRequest request, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);
        var project = await RequireAsync(id, cancellationToken);
        project.Update(
            request.DepartmentId,
            request.OwnerUserId,
            request.Code,
            request.Name,
            request.Description,
            EnumCodes.ParseOr(request.Status, project.Status),
            request.StartDate,
            request.EndDate,
            request.Budget);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(project);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        projects.Remove(await RequireAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Project> RequireAsync(long id, CancellationToken cancellationToken) =>
        await projects.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Project", id);

    private static ProjectDto ToDto(Project project) => new(
        project.Id,
        project.DepartmentId,
        project.OwnerUserId,
        project.Code,
        project.Name,
        project.Description,
        project.Status.ToCode(),
        project.StartDate,
        project.EndDate,
        project.Budget,
        project.CreatedAt,
        project.UpdatedAt);
}
