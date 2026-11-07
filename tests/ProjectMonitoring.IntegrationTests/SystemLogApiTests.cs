using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace ProjectMonitoring.IntegrationTests;

public class SystemLogApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private const string Logs = "/api/v1/logs";

    /// <summary>The log is written as the response finishes, so a read can briefly arrive first.</summary>
    private async Task<JsonElement> WaitForLogAsync(string query, Func<JsonElement, bool> predicate)
    {
        for (var attempt = 0; ; attempt++)
        {
            var response = await api.Admin.GetAsync($"{Logs}?pageSize=100&{query}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            var match = body.GetProperty("items").EnumerateArray().Where(predicate).ToList();
            if (match.Count > 0)
            {
                return match[0];
            }

            Assert.True(attempt < 30, $"No log matched for '{query}'.");
            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task Long_custom_methods_are_still_logged()
    {
        using var request = new HttpRequestMessage(new HttpMethod("VERSION-CONTROL"), "/api/v1/departments/424242");
        await api.Anonymous.SendAsync(request);

        var log = await WaitForLogAsync(
            "eventType=http_request", item => item.GetProperty("path").GetString() == "/api/v1/departments/424242");
        Assert.Equal("VERSION-CO", log.GetProperty("httpMethod").GetString());
    }

    [Fact]
    public async Task Requests_with_a_deleted_account_token_are_rejected_and_logged()
    {
        var created = await api.Admin.PostAsJsonAsync("/api/v1/users", new
        {
            departmentId = 1, fullName = "Deleted", email = "deleted-log@example.com", password = "deleted-log-1",
        });
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64();
        var user = await api.LogInAsync("deleted-log@example.com", "deleted-log-1");
        await api.Admin.DeleteAsync($"/api/v1/users/{id}");

        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync("/api/v1/departments/434343")).StatusCode);
        var log = await WaitForLogAsync(
            "eventType=http_request", item => item.GetProperty("path").GetString() == "/api/v1/departments/434343");
        Assert.Equal(401, log.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Requests_are_logged_with_user_and_status_but_no_query_string()
    {
        var response = await api.Manager.GetAsync("/api/v1/projects/1?marker=query-secret");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var log = await WaitForLogAsync(
            "eventType=http_request&level=info",
            item => item.GetProperty("path").GetString() == "/api/v1/projects/1");

        Assert.Equal("GET", log.GetProperty("httpMethod").GetString());
        Assert.Equal(200, log.GetProperty("statusCode").GetInt32());
        Assert.True(log.GetProperty("durationMs").GetInt32() >= 0);
        Assert.NotEqual(JsonValueKind.Null, log.GetProperty("userId").ValueKind);
        Assert.False(string.IsNullOrEmpty(log.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("query-secret", log.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failed_login_keeps_neither_password_nor_email()
    {
        const string password = "typed-password-value";
        const string email = "typed-email@example.com";
        var response = await api.Anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var log = await WaitForLogAsync("eventType=login_failed", _ => true);
        Assert.Equal("warning", log.GetProperty("level").GetString());
        Assert.Equal(JsonValueKind.Null, log.GetProperty("userId").ValueKind);

        var dump = await api.Admin.GetStringAsync($"{Logs}?pageSize=100");
        Assert.DoesNotContain(password, dump, StringComparison.Ordinal);
        Assert.DoesNotContain(email, dump, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logs_never_contain_tokens_or_password_hashes()
    {
        await api.Admin.PostAsJsonAsync("/api/v1/users", new
        {
            departmentId = 1,
            fullName = "Log Check",
            email = "logcheck@example.com",
            password = "log-check-password",
        });
        await WaitForLogAsync(
            "eventType=http_request",
            item => item.GetProperty("path").GetString() == "/api/v1/users");

        var dump = await api.Admin.GetStringAsync($"{Logs}?pageSize=100");
        Assert.DoesNotContain("log-check-password", dump, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJ", dump, StringComparison.Ordinal); // JWT prefix
        Assert.DoesNotContain("$2a$", dump, StringComparison.Ordinal); // BCrypt prefix
    }

    [Fact]
    public async Task Unexpected_errors_are_logged_with_details_only_for_admins()
    {
        var response = await api.Admin.GetAsync("/test/boom");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(
            "secret internal detail", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var log = await WaitForLogAsync("eventType=exception&level=error", _ => true);
        var details = log.GetProperty("details");
        Assert.Equal("System.InvalidOperationException", details.GetProperty("exceptionType").GetString());
        Assert.Equal("secret internal detail", details.GetProperty("message").GetString());
        Assert.Equal(500, log.GetProperty("statusCode").GetInt32());

        await WaitForLogAsync(
            "eventType=http_request&level=error",
            item => item.GetProperty("path").GetString() == "/test/boom");
    }

    [Fact]
    public async Task Rejected_requests_are_warnings_without_details()
    {
        await api.Admin.GetAsync("/api/v1/tasks/999999");
        var log = await WaitForLogAsync(
            "eventType=request_rejected&level=warning",
            item => item.GetProperty("path").GetString() == "/api/v1/tasks/999999");
        Assert.Equal(404, log.GetProperty("statusCode").GetInt32());
        Assert.Equal("not_found", log.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, log.GetProperty("details").ValueKind);
    }

    [Fact]
    public async Task Health_checks_are_not_logged()
    {
        Assert.Equal(HttpStatusCode.OK, (await api.Anonymous.GetAsync("/health")).StatusCode);
        await api.Admin.GetAsync("/api/v1/departments/1"); // proves logging is running
        await WaitForLogAsync(
            "eventType=http_request",
            item => item.GetProperty("path").GetString() == "/api/v1/departments/1");

        var dump = await api.Admin.GetStringAsync($"{Logs}?pageSize=100");
        Assert.DoesNotContain("\"/health\"", dump, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Time_filter_paging_and_newest_first()
    {
        await api.Admin.GetAsync("/api/v1/departments/2");
        await WaitForLogAsync("eventType=http_request", item => item.GetProperty("path").GetString() == "/api/v1/departments/2");

        var future = DateTimeOffset.UtcNow.AddHours(1).ToString("O");
        var empty = await api.Admin.GetFromJsonAsync<JsonElement>(QueryHelpers.AddQueryString(Logs, "from", future));
        Assert.Equal(0, empty.GetProperty("totalItems").GetInt32());

        // A local offset must be converted, since timestamptz parameters have to be UTC.
        var past = DateTimeOffset.UtcNow.AddHours(-1).ToOffset(TimeSpan.FromHours(7)).ToString("O");
        var page = await api.Admin.GetFromJsonAsync<JsonElement>(
            QueryHelpers.AddQueryString(Logs, new Dictionary<string, string?> { ["from"] = past, ["pageSize"] = "5" }));
        Assert.True(page.GetProperty("items").GetArrayLength() > 0);

        var times = page.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("createdAt").GetDateTimeOffset()).ToList();
        Assert.Equal(times.OrderByDescending(time => time), times);
    }

    [Theory]
    [InlineData("level=fatal", "level")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z", "to")]
    public async Task Invalid_filters_are_400(string query, string field)
    {
        var response = await api.Admin.GetAsync($"{Logs}?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_error", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Only_admins_can_read_logs()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous.GetAsync(Logs)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Member.GetAsync(Logs)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Manager.GetAsync(Logs)).StatusCode);
    }
}
