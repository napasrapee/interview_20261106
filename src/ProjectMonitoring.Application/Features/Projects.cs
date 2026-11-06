using FluentValidation;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features;

public sealed record ProjectRequest(
    long DepartmentId,
    string Code,
    string Name,
    long? OwnerUserId = null,
    string? Description = null,
    string? Status = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    decimal? Budget = null);

public sealed record ProjectDto(
    long Id,
    long DepartmentId,
    long? OwnerUserId,
    string Code,
    string Name,
    string? Description,
    string Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? Budget,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProjectListQuery(long? DepartmentId = null, string? Status = null, int Page = 1, int PageSize = 20)
    : PageQuery(Page, PageSize);

public sealed class ProjectRequestValidator : AbstractValidator<ProjectRequest>
{
    public ProjectRequestValidator()
    {
        RuleFor(request => request.DepartmentId).GreaterThan(0);
        RuleFor(request => request.OwnerUserId).GreaterThan(0).When(request => request.OwnerUserId is not null);
        RuleFor(request => request.Code).NotEmpty().MaximumLength(30);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Status)
            .Must(EnumCodes.IsValid<ProjectStatus>)
            .WithMessage("Status must be planned, active, on_hold, completed or cancelled.")
            .When(request => request.Status is not null);
        RuleFor(request => request.EndDate)
            .GreaterThanOrEqualTo(request => request.StartDate)
            .When(request => request.StartDate is not null && request.EndDate is not null);
        RuleFor(request => request.Budget)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(14, 2, ignoreTrailingZeros: true)
            .When(request => request.Budget is not null);
    }
}

public sealed class ProjectListQueryValidator : AbstractValidator<ProjectListQuery>
{
    public ProjectListQueryValidator()
    {
        this.ApplyPageRules();
        RuleFor(query => query.Status)
            .Must(EnumCodes.IsValid<ProjectStatus>)
            .WithMessage("Status must be planned, active, on_hold, completed or cancelled.")
            .When(query => query.Status is not null);
    }
}

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
