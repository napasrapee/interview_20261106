using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectMonitoring.IntegrationTests;

public static class ApiFactory
{
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "admin-password-1";
    public const string Secret = "integration-test-secret-integration-test-secret";

    public static WebApplicationFactory<Program> Create(
        string connectionString, int loginLimit = 100_000, Assembly? extraControllers = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Jwt:Secret", Secret);
            builder.UseSetting("Jwt:Issuer", "test-issuer");
            builder.UseSetting("Jwt:Audience", "test-audience");
            builder.UseSetting("Jwt:ExpiresMinutes", "60");
            builder.UseSetting("Admin:Email", AdminEmail);
            builder.UseSetting("Admin:Password", AdminPassword);
            builder.UseSetting("RateLimit:LoginPermitLimit", loginLimit.ToString());
            if (extraControllers is not null)
            {
                builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(extraControllers));
            }
        });
}

/// <summary>Shares one API, database and set of accounts for each role across the CRUD tests.</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private const string Password = "user-password-1";
    private readonly DatabaseFixture _database = new();
    private WebApplicationFactory<Program> _factory = null!;

    public HttpClient Anonymous { get; private set; } = null!;
    public HttpClient Admin { get; private set; } = null!;
    public HttpClient Manager { get; private set; } = null!;
    public HttpClient Member { get; private set; } = null!;
    public long MemberId { get; private set; }
    public long AdminId { get; private set; }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _factory = ApiFactory.Create(_database.ConnectionString, extraControllers: typeof(PolicyProbeController).Assembly);
        Anonymous = _factory.CreateClient();
        Admin = await LogInAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);
        AdminId = (await (await Admin.GetAsync("/api/v1/auth/me")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetInt64();

        await CreateUserAsync("fixture-manager@example.com", "manager");
        MemberId = await CreateUserAsync("fixture-member@example.com", "member");
        Manager = await LogInAsync("fixture-manager@example.com", Password);
        Member = await LogInAsync("fixture-member@example.com", Password);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _database.DisposeAsync();
    }

    public async Task<HttpClient> LogInAsync(string email, string password)
    {
        var response = await Anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<long> CreateUserAsync(string email, string role)
    {
        var response = await Admin.PostAsJsonAsync("/api/v1/users", new
        {
            departmentId = 1,
            fullName = "Fixture " + role,
            email,
            password = Password,
            role,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }
}

public class CrudApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("   at ", text, StringComparison.Ordinal);
        return body;
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task<long> CreateAsync(HttpClient client, string path, object payload)
    {
        var response = await client.PostAsJsonAsync(path, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await BodyAsync(response)).GetProperty("id").GetInt64();
    }

    private static HttpRequestMessage Message(string method, string path) =>
        new(new HttpMethod(method), path)
        {
            Content = method is "GET" or "DELETE" ? null : new StringContent("{}", Encoding.UTF8, "application/json"),
        };

    // Every protected route, with the lowest role that may use it. Bodies are empty on purpose:
    // authorization runs before validation, so permitted calls are not asserted on status here.
    public static TheoryData<string, string, string> Routes => new()
    {
        { "GET", "/api/v1/auth/me", "member" },
        { "GET", "/api/v1/departments", "member" },
        { "GET", "/api/v1/departments/1", "member" },
        { "POST", "/api/v1/departments", "admin" },
        { "PUT", "/api/v1/departments/1", "admin" },
        { "DELETE", "/api/v1/departments/999999", "admin" },
        { "GET", "/api/v1/users", "manager" },
        { "GET", "/api/v1/users/1", "manager" },
        { "POST", "/api/v1/users", "admin" },
        { "PUT", "/api/v1/users/1", "admin" },
        { "DELETE", "/api/v1/users/999999", "admin" },
        { "GET", "/api/v1/projects", "member" },
        { "GET", "/api/v1/projects/1", "member" },
        { "POST", "/api/v1/projects", "manager" },
        { "PUT", "/api/v1/projects/1", "manager" },
        { "DELETE", "/api/v1/projects/999999", "manager" },
        { "GET", "/api/v1/tasks", "member" },
        { "GET", "/api/v1/tasks/1", "member" },
        { "POST", "/api/v1/tasks", "manager" },
        { "PUT", "/api/v1/tasks/1", "manager" },
        { "PATCH", "/api/v1/tasks/1/status", "member" },
        { "DELETE", "/api/v1/tasks/999999", "manager" },
        { "GET", "/api/v1/reports/department-progress", "manager" },
        { "GET", "/api/v1/reports/project-summary", "manager" },
        { "GET", "/api/v1/reports/user-workload", "manager" },
        { "GET", "/api/v1/reports/department-progress/export?format=csv", "manager" },
        { "GET", "/api/v1/logs", "admin" },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Permissions_match_the_specification(string method, string path, string lowestRole)
    {
        using var noToken = Message(method, path);
        await AssertProblemAsync(await api.Anonymous.SendAsync(noToken), HttpStatusCode.Unauthorized, "unauthorized");

        var ranks = new[] { ("member", api.Member), ("manager", api.Manager), ("admin", api.Admin) };
        var allowed = false;
        foreach (var (role, client) in ranks)
        {
            allowed |= role == lowestRole;
            using var request = Message(method, path);
            var response = await client.SendAsync(request);
            if (allowed)
            {
                Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
                Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            }
            else
            {
                await AssertProblemAsync(response, HttpStatusCode.Forbidden, "forbidden");
            }
        }
    }

    [Fact]
    public async Task Department_lifecycle()
    {
        var created = await api.Admin.PostAsJsonAsync("/api/v1/departments", new { code = "LIFE", name = "วงจรชีวิต" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await BodyAsync(created);
        var id = body.GetProperty("id").GetInt64();
        Assert.Equal("วงจรชีวิต", body.GetProperty("name").GetString());
        Assert.EndsWith($"/api/v1/departments/{id}", created.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        var fetched = await api.Member.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

        var updated = await api.Admin.PutAsJsonAsync($"/api/v1/departments/{id}", new { code = "LIFE", name = "New" });
        Assert.Equal("New", (await BodyAsync(updated)).GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await api.Admin.DeleteAsync($"/api/v1/departments/{id}")).StatusCode);
        await AssertProblemAsync(await api.Admin.GetAsync($"/api/v1/departments/{id}"), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Project_lifecycle_with_filters_and_cascade()
    {
        var id = await CreateAsync(api.Manager, "/api/v1/projects", new
        {
            departmentId = 4,
            code = "PRJ-LIFE",
            name = "Life",
            status = "on_hold",
            startDate = "2026-01-01",
            endDate = "2026-01-01",
            budget = 1234.5,
        });

        var filtered = await BodyAsync(await api.Member.GetAsync("/api/v1/projects?status=on_hold&departmentId=4"));
        Assert.Equal(1, filtered.GetProperty("totalItems").GetInt32());
        var item = filtered.GetProperty("items")[0];
        Assert.Equal("2026-01-01", item.GetProperty("startDate").GetString());
        Assert.Equal(1234.5m, item.GetProperty("budget").GetDecimal());

        var taskId = await CreateAsync(api.Manager, "/api/v1/tasks", new { projectId = id, title = "Child" });
        Assert.Equal(HttpStatusCode.NoContent, (await api.Manager.DeleteAsync($"/api/v1/projects/{id}")).StatusCode);
        await AssertProblemAsync(await api.Manager.GetAsync($"/api/v1/tasks/{taskId}"), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Task_status_flow_and_defaults()
    {
        var created = await BodyAsync(await api.Manager.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = 1,
            assigneeUserId = api.MemberId,
            title = "Flow",
        }));
        var id = created.GetProperty("id").GetInt64();
        Assert.Equal("todo", created.GetProperty("status").GetString());
        Assert.Equal("medium", created.GetProperty("priority").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("completedAt").ValueKind);

        var done = await api.Member.PatchAsJsonAsync($"/api/v1/tasks/{id}/status", new { status = "done" });
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        Assert.NotEqual(JsonValueKind.Null, (await BodyAsync(done)).GetProperty("completedAt").ValueKind);

        var reopened = await api.Member.PatchAsJsonAsync($"/api/v1/tasks/{id}/status", new { status = "in_progress" });
        Assert.Equal(JsonValueKind.Null, (await BodyAsync(reopened)).GetProperty("completedAt").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await api.Manager.DeleteAsync($"/api/v1/tasks/{id}")).StatusCode);
    }

    [Fact]
    public async Task Member_cannot_change_status_of_other_tasks()
    {
        var response = await api.Member.PatchAsJsonAsync("/api/v1/tasks/1/status", new { status = "done" });
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Created_user_logs_in_and_keeps_password_on_update()
    {
        var created = await api.Admin.PostAsJsonAsync("/api/v1/users", new
        {
            departmentId = 2,
            fullName = "ทดสอบ",
            email = "lifecycle@example.com",
            password = "first-password",
        });
        var text = await created.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        var id = JsonDocument.Parse(text).RootElement.GetProperty("id").GetInt64();
        Assert.Equal("member", JsonDocument.Parse(text).RootElement.GetProperty("role").GetString());

        var update = new { departmentId = 2, fullName = "Renamed", email = "lifecycle@example.com" };
        Assert.Equal(HttpStatusCode.OK, (await api.Admin.PutAsJsonAsync($"/api/v1/users/{id}", update)).StatusCode);
        await api.LogInAsync("lifecycle@example.com", "first-password");

        var deactivate = new { departmentId = 2, fullName = "Renamed", email = "lifecycle@example.com", isActive = false };
        await api.Admin.PutAsJsonAsync($"/api/v1/users/{id}", deactivate);
        var login = await api.Anonymous.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = "lifecycle@example.com", password = "first-password" });
        await AssertProblemAsync(login, HttpStatusCode.Unauthorized, "invalid_credentials");

        Assert.Equal(HttpStatusCode.NoContent, (await api.Admin.DeleteAsync($"/api/v1/users/{id}")).StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_remove_own_access()
    {
        var path = $"/api/v1/users/{api.AdminId}";
        var me = await BodyAsync(await api.Admin.GetAsync(path));
        var departmentId = me.GetProperty("departmentId").GetInt64();
        var email = me.GetProperty("email").GetString();

        await AssertProblemAsync(await api.Admin.DeleteAsync(path), HttpStatusCode.Conflict, "conflict");
        await AssertProblemAsync(
            await api.Admin.PutAsJsonAsync(path, new { departmentId, fullName = "A", email, role = "admin", isActive = false }),
            HttpStatusCode.Conflict, "conflict");
        await AssertProblemAsync(
            await api.Admin.PutAsJsonAsync(path, new { departmentId, fullName = "A", email, role = "member" }),
            HttpStatusCode.Conflict, "conflict");
    }

    public static TheoryData<string, string> InvalidQueries => new()
    {
        { "/api/v1/departments?page=0", "page" },
        { "/api/v1/departments?pageSize=101", "pageSize" },
        { "/api/v1/users?role=root", "role" },
        { "/api/v1/projects?status=closed", "status" },
        { "/api/v1/tasks?status=closed", "status" },
        { "/api/v1/tasks?page=abc", "page" },
    };

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Invalid_queries_are_400(string path, string field)
    {
        var body = await AssertProblemAsync(await api.Admin.GetAsync(path), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Paging_shape_matches_the_specification()
    {
        var body = await BodyAsync(await api.Member.GetAsync("/api/v1/departments?page=1&pageSize=2"));
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
        Assert.True(body.GetProperty("totalItems").GetInt32() >= 4);
        Assert.True(body.GetProperty("totalPages").GetInt32() >= 2);
    }

    [Fact]
    public async Task Invalid_bodies_are_400()
    {
        var dates = await api.Manager.PostAsJsonAsync("/api/v1/projects", new
        {
            departmentId = 1,
            code = "PRJ-BAD",
            name = "Bad",
            startDate = "2026-02-01",
            endDate = "2026-01-01",
        });
        var body = await AssertProblemAsync(dates, HttpStatusCode.BadRequest, "validation_error");
        Assert.True(body.GetProperty("errors").TryGetProperty("endDate", out _));

        await AssertProblemAsync(
            await api.Admin.PostAsJsonAsync("/api/v1/departments", new { name = "Missing code" }),
            HttpStatusCode.BadRequest, "validation_error");
        await AssertProblemAsync(
            await api.Admin.PostAsJsonAsync("/api/v1/users", new { departmentId = 1, fullName = "A", email = "a@example.com", password = "short" }),
            HttpStatusCode.BadRequest, "validation_error");
        await AssertProblemAsync(
            await api.Admin.PostAsync("/api/v1/departments", new StringContent("{broken", Encoding.UTF8, "application/json")),
            HttpStatusCode.BadRequest, "malformed_request");
    }

    [Fact]
    public async Task Put_without_optional_fields_keeps_current_values()
    {
        var userId = await CreateAsync(api.Admin, "/api/v1/users", new
        {
            departmentId = 2,
            fullName = "Before",
            email = "keep-values@example.com",
            role = "manager",
            isActive = false,
        });
        var user = await BodyAsync(await api.Admin.PutAsJsonAsync(
            $"/api/v1/users/{userId}", new { departmentId = 2, fullName = "After", email = "keep-values@example.com" }));
        Assert.Equal("After", user.GetProperty("fullName").GetString());
        Assert.Equal("manager", user.GetProperty("role").GetString());
        Assert.False(user.GetProperty("isActive").GetBoolean());

        var projectId = await CreateAsync(api.Manager, "/api/v1/projects", new
        {
            departmentId = 2,
            code = "PRJ-KEEP",
            name = "Before",
            status = "on_hold",
        });
        var project = await BodyAsync(await api.Manager.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}", new { departmentId = 2, code = "PRJ-KEEP", name = "After" }));
        Assert.Equal("on_hold", project.GetProperty("status").GetString());

        var task = await BodyAsync(await api.Manager.PostAsJsonAsync(
            "/api/v1/tasks", new { projectId, title = "Before", status = "done", priority = "high" }));
        var taskId = task.GetProperty("id").GetInt64();
        var stored = await BodyAsync(await api.Manager.GetAsync($"/api/v1/tasks/{taskId}"));
        var updated = await BodyAsync(await api.Manager.PutAsJsonAsync(
            $"/api/v1/tasks/{taskId}", new { projectId, title = "After" }));
        Assert.Equal("done", updated.GetProperty("status").GetString());
        Assert.Equal("high", updated.GetProperty("priority").GetString());
        Assert.Equal(
            stored.GetProperty("completedAt").GetDateTimeOffset(),
            updated.GetProperty("completedAt").GetDateTimeOffset());

        var reopened = await BodyAsync(await api.Manager.PutAsJsonAsync(
            $"/api/v1/tasks/{taskId}", new { projectId, title = "After", status = "todo" }));
        Assert.Equal("todo", reopened.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, reopened.GetProperty("completedAt").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await api.Manager.DeleteAsync($"/api/v1/projects/{projectId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.Admin.DeleteAsync($"/api/v1/users/{userId}")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_rename_own_account_without_sending_role()
    {
        var path = $"/api/v1/users/{api.AdminId}";
        var me = await BodyAsync(await api.Admin.GetAsync(path));

        var response = await api.Admin.PutAsJsonAsync(path, new
        {
            departmentId = me.GetProperty("departmentId").GetInt64(),
            fullName = me.GetProperty("fullName").GetString(),
            email = me.GetProperty("email").GetString(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("admin", (await BodyAsync(response)).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Database_outage_is_503_and_other_database_errors_are_500()
    {
        var outage = await AssertProblemAsync(
            await api.Admin.GetAsync("/test/database-outage"),
            HttpStatusCode.ServiceUnavailable, "database_unavailable");
        Assert.DoesNotContain("secret database detail", outage.GetRawText(), StringComparison.Ordinal);

        await AssertProblemAsync(
            await api.Admin.GetAsync("/test/database-error"),
            HttpStatusCode.InternalServerError, "internal_error");
    }

    [Fact]
    public async Task Null_characters_are_rejected_instead_of_failing_in_the_database()
    {
        await AssertProblemAsync(
            await api.Admin.PostAsync(
                "/api/v1/departments",
                new StringContent("""{"code":"NUL","name":"a\u0000b"}""", Encoding.UTF8, "application/json")),
            HttpStatusCode.BadRequest, "malformed_request");
        await AssertProblemAsync(
            await api.Anonymous.PostAsync(
                "/api/v1/auth/login",
                new StringContent("""{"email":"a\u0000@example.com","password":"x"}""", Encoding.UTF8, "application/json")),
            HttpStatusCode.BadRequest, "malformed_request");
        await AssertProblemAsync(
            await api.Admin.GetAsync("/api/v1/logs?eventType=a%00b"),
            HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task Database_conflicts_are_409()
    {
        await AssertProblemAsync(
            await api.Admin.PostAsJsonAsync("/api/v1/departments", new { code = "IT", name = "Copy" }),
            HttpStatusCode.Conflict, "conflict");
        await AssertProblemAsync(await api.Admin.DeleteAsync("/api/v1/departments/1"), HttpStatusCode.Conflict, "conflict");
        await AssertProblemAsync(
            await api.Manager.PostAsJsonAsync("/api/v1/tasks", new { projectId = 999999, title = "Orphan" }),
            HttpStatusCode.Conflict, "conflict");
        await AssertProblemAsync(
            await api.Admin.PostAsJsonAsync("/api/v1/users", new { departmentId = 1, fullName = "Copy", email = "SOMCHAI@example.com" }),
            HttpStatusCode.Conflict, "conflict");

        var body = await BodyAsync(await api.Admin.DeleteAsync("/api/v1/users/1"));
        Assert.DoesNotContain("violates", body.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("users_", body.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_records_are_404()
    {
        foreach (var resource in new[] { "departments", "users", "projects", "tasks" })
        {
            await AssertProblemAsync(
                await api.Admin.GetAsync($"/api/v1/{resource}/999999"), HttpStatusCode.NotFound, "not_found");
            await AssertProblemAsync(
                await api.Admin.DeleteAsync($"/api/v1/{resource}/999999"), HttpStatusCode.NotFound, "not_found");
        }
    }

    [Fact]
    public async Task Framework_errors_use_the_same_format()
    {
        await AssertProblemAsync(await api.Admin.GetAsync("/api/v1/nothing"), HttpStatusCode.NotFound, "not_found");
        await AssertProblemAsync(await api.Admin.GetAsync("/api/v1/tasks/abc"), HttpStatusCode.NotFound, "not_found");
        await AssertProblemAsync(
            await api.Admin.PatchAsync("/api/v1/departments", new StringContent("{}", Encoding.UTF8, "application/json")),
            HttpStatusCode.MethodNotAllowed, "method_not_allowed");
        await AssertProblemAsync(
            await api.Admin.PostAsync("/api/v1/departments", new StringContent("code=IT", Encoding.UTF8, "text/plain")),
            HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
    }
}
