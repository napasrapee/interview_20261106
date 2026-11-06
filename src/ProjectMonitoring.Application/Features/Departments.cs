using FluentValidation;
using ProjectMonitoring.Application.Abstractions;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features;

public sealed record DepartmentRequest(string Code, string Name);

public sealed record DepartmentDto(
    long Id, string Code, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed class DepartmentRequestValidator : AbstractValidator<DepartmentRequest>
{
    public DepartmentRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(20);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(150);
    }
}

public sealed class PageQueryValidator : AbstractValidator<PageQuery>
{
    public PageQueryValidator() => this.ApplyPageRules();
}

public sealed class DepartmentService(
    IDepartmentRepository departments,
    IUnitOfWork unitOfWork,
    IValidator<DepartmentRequest> validator,
    IValidator<PageQuery> pageValidator)
{
    public async Task<PagedResult<DepartmentDto>> ListAsync(PageQuery query, CancellationToken cancellationToken)
    {
        await pageValidator.EnsureValidAsync(query, cancellationToken);
        return (await departments.ListAsync(query, cancellationToken)).Map(ToDto);
    }

    public async Task<DepartmentDto> GetAsync(long id, CancellationToken cancellationToken) =>
        ToDto(await RequireAsync(id, cancellationToken));

    public async Task<DepartmentDto> CreateAsync(DepartmentRequest request, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);
        var department = new Department(request.Code, request.Name);
        departments.Add(department);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(department);
    }

    public async Task<DepartmentDto> UpdateAsync(long id, DepartmentRequest request, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);
        var department = await RequireAsync(id, cancellationToken);
        department.Update(request.Code, request.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(department);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        departments.Remove(await RequireAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Department> RequireAsync(long id, CancellationToken cancellationToken) =>
        await departments.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Department", id);

    private static DepartmentDto ToDto(Department department) => new(
        department.Id, department.Code, department.Name, department.CreatedAt, department.UpdatedAt);
}
