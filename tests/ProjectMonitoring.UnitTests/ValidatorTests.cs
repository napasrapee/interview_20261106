using ProjectMonitoring.Application.Features.Projects;
using ProjectMonitoring.Application.Features.Tasks;
using ProjectMonitoring.Application.Features.Users;

namespace ProjectMonitoring.UnitTests;

public class ValidatorTests
{
    private static readonly UserRequest ValidUser = new(1, "Somchai", "somchai@example.com", "password1");

    [Fact]
    public void Valid_user_passes() =>
        Assert.True(new UserRequestValidator().Validate(ValidUser).IsValid);

    [Fact]
    public void User_without_password_passes() =>
        Assert.True(new UserRequestValidator().Validate(ValidUser with { Password = null }).IsValid);

    [Theory]
    [InlineData("short")]
    [InlineData("รหัสผ่านภาษาไทยที่ยาวมากจนเกินเจ็ดสิบสองไบต์เมื่อเข้ารหัสยูทีเอฟแปด")]
    public void Invalid_password_fails(string password) =>
        Assert.False(new UserRequestValidator().Validate(ValidUser with { Password = password }).IsValid);

    [Fact]
    public void Invalid_email_and_role_fail()
    {
        var result = new UserRequestValidator().Validate(ValidUser with { Email = "nope", Role = "root" });
        Assert.Contains(result.Errors, error => error.PropertyName == "Email");
        Assert.Contains(result.Errors, error => error.PropertyName == "Role");
    }

    [Fact]
    public void Project_end_before_start_fails()
    {
        var request = new ProjectRequest(
            1, "P", "P", StartDate: new DateOnly(2026, 2, 1), EndDate: new DateOnly(2026, 1, 1));
        Assert.Contains(
            new ProjectRequestValidator().Validate(request).Errors,
            error => error.PropertyName == "EndDate");
    }

    [Fact]
    public void Project_same_start_and_end_passes()
    {
        var date = new DateOnly(2026, 1, 1);
        Assert.True(new ProjectRequestValidator().Validate(
            new ProjectRequest(1, "P", "P", StartDate: date, EndDate: date)).IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.234)]
    public void Invalid_budget_fails(double budget) =>
        Assert.False(new ProjectRequestValidator().Validate(
            new ProjectRequest(1, "P", "P", Budget: (decimal)budget)).IsValid);

    [Theory]
    [InlineData("closed")]
    [InlineData("")]
    public void Invalid_task_status_fails(string status) =>
        Assert.False(new TaskStatusRequestValidator().Validate(new TaskStatusRequest(status)).IsValid);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(1_000_001, 20)]
    [InlineData(int.MaxValue, 100)]
    public void Invalid_paging_fails(int page, int pageSize) =>
        Assert.False(new TaskListQueryValidator().Validate(new TaskListQuery(Page: page, PageSize: pageSize)).IsValid);

    [Fact]
    public void Maximum_page_size_passes() =>
        Assert.True(new TaskListQueryValidator().Validate(new TaskListQuery(PageSize: 100)).IsValid);
}
