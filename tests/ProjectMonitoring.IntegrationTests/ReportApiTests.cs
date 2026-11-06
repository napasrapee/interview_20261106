using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ProjectMonitoring.IntegrationTests;

/// <summary>Compares report results with values worked out by hand from db/init/02_seed.sql.</summary>
public class ReportApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private const string Base = "/api/v1/reports";

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private static JsonElement Row(JsonElement rows, string key, string value) =>
        rows.EnumerateArray().Single(row => row.GetProperty(key).GetString() == value);

    private static void AssertCounts(JsonElement row, string name, long expected) =>
        Assert.Equal(expected, row.GetProperty(name).GetInt64());

    [Fact]
    public async Task Department_progress_matches_seed()
    {
        var rows = await JsonAsync(await api.Manager.GetAsync($"{Base}/department-progress"));
        Assert.Equal(4, rows.GetArrayLength());

        var hr = rows[0];
        Assert.Equal("HR", hr.GetProperty("departmentCode").GetString());
        Assert.Equal(75.00m, hr.GetProperty("progressPercent").GetDecimal());
        AssertCounts(hr, "progressRank", 1);
        AssertCounts(hr, "totalTasks", 4);
        AssertCounts(hr, "doneTasks", 3);
        AssertCounts(hr, "overdueTasks", 0);

        var it = rows[1];
        Assert.Equal("IT", it.GetProperty("departmentCode").GetString());
        Assert.Equal("เทคโนโลยีสารสนเทศ", it.GetProperty("departmentName").GetString());
        Assert.Equal(40.00m, it.GetProperty("progressPercent").GetDecimal());
        AssertCounts(it, "progressRank", 2);
        AssertCounts(it, "projectCount", 2);
        AssertCounts(it, "totalTasks", 10);
        AssertCounts(it, "doneTasks", 4);
        AssertCounts(it, "inProgressTasks", 3);
        AssertCounts(it, "todoTasks", 3);
        AssertCounts(it, "overdueTasks", 1);

        // FN has only a cancelled project with tasks plus an empty one; MK has no projects.
        var fn = Row(rows, "departmentCode", "FN");
        AssertCounts(fn, "projectCount", 1);
        AssertCounts(fn, "totalTasks", 0);
        Assert.Equal(0m, fn.GetProperty("progressPercent").GetDecimal());
        AssertCounts(fn, "progressRank", 3);

        var mk = Row(rows, "departmentCode", "MK");
        AssertCounts(mk, "projectCount", 0);
        Assert.Equal(0m, mk.GetProperty("progressPercent").GetDecimal());
    }

    [Fact]
    public async Task Project_summary_matches_seed_and_excludes_cancelled()
    {
        var rows = await JsonAsync(await api.Admin.GetAsync($"{Base}/project-summary"));
        Assert.Equal(4, rows.GetArrayLength());
        Assert.DoesNotContain(rows.EnumerateArray(), row => row.GetProperty("projectCode").GetString() == "PRJ-FN-01");

        var first = Row(rows, "projectCode", "PRJ-IT-01");
        AssertCounts(first, "totalTasks", 6);
        AssertCounts(first, "doneTasks", 3);
        AssertCounts(first, "overdueTasks", 1);
        Assert.Equal(50.00m, first.GetProperty("progressPercent").GetDecimal());
        Assert.Equal("สมชาย ใจดี", first.GetProperty("ownerName").GetString());
        Assert.Equal("2026-09-01", first.GetProperty("startDate").GetString());
        Assert.Equal(250000m, first.GetProperty("budget").GetDecimal());

        Assert.Equal(25.00m, Row(rows, "projectCode", "PRJ-IT-02").GetProperty("progressPercent").GetDecimal());
        Assert.Equal(0m, Row(rows, "projectCode", "PRJ-FN-02").GetProperty("progressPercent").GetDecimal());

        var filtered = await JsonAsync(await api.Admin.GetAsync($"{Base}/project-summary?departmentId=2"));
        Assert.Equal("PRJ-HR-01", Assert.Single(filtered.EnumerateArray()).GetProperty("projectCode").GetString());
    }

    [Fact]
    public async Task User_workload_matches_seed()
    {
        var rows = await JsonAsync(await api.Manager.GetAsync($"{Base}/user-workload?departmentId=1"));

        var wipa = Row(rows, "email", "wipa@example.com");
        AssertCounts(wipa, "totalTasks", 5);
        AssertCounts(wipa, "doneTasks", 3);
        AssertCounts(wipa, "openTasks", 2);
        AssertCounts(wipa, "overdueTasks", 0);

        var thana = Row(rows, "email", "thana@example.com");
        AssertCounts(thana, "totalTasks", 4);
        AssertCounts(thana, "inProgressTasks", 2);
        AssertCounts(thana, "overdueTasks", 1);

        // Owner with no tasks still appears, and tasks of cancelled projects are not counted.
        AssertCounts(Row(rows, "email", "somchai@example.com"), "totalTasks", 0);
        var finance = await JsonAsync(await api.Manager.GetAsync($"{Base}/user-workload?departmentId=3"));
        AssertCounts(Assert.Single(finance.EnumerateArray()), "totalTasks", 0);
    }

    [Theory]
    [InlineData("department-progress", "")]
    [InlineData("project-summary", "&departmentId=1")]
    [InlineData("user-workload", "&departmentId=1")]
    public async Task Csv_and_json_exports_contain_the_same_data(string report, string filter)
    {
        var jsonResponse = await api.Manager.GetAsync($"{Base}/{report}/export?format=json{filter}");
        Assert.Equal("application/json", jsonResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", jsonResponse.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith(".json", jsonResponse.Content.Headers.ContentDisposition?.FileName, StringComparison.Ordinal);
        var json = JsonDocument.Parse(await jsonResponse.Content.ReadAsStringAsync()).RootElement;

        var csvResponse = await api.Manager.GetAsync($"{Base}/{report}/export?format=csv{filter}");
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", csvResponse.Content.Headers.ContentDisposition?.DispositionType);
        Assert.StartsWith(report + "-", csvResponse.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.Ordinal);
        var bytes = await csvResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);

        var lines = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var header = lines[0].Split(',');
        Assert.Equal(json.GetArrayLength(), lines.Length - 1);

        for (var row = 0; row < json.GetArrayLength(); row++)
        {
            // Seed values contain no commas or quotes, so a plain split is safe here.
            var cells = lines[row + 1].Split(',');
            Assert.Equal(header.Length, cells.Length);
            for (var column = 0; column < header.Length; column++)
            {
                var value = json[row].GetProperty(header[column]);
                var expected = value.ValueKind switch
                {
                    JsonValueKind.Null => "",
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => value.GetRawText(),
                };
                Assert.Equal(expected, cells[column]);
            }
        }
    }

    [Fact]
    public async Task Formula_in_data_is_neutralized_in_export()
    {
        var created = await api.Admin.PostAsJsonAsync("/api/v1/departments", new { code = "=CMD", name = "@evil,\"x\"" });
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64();
        try
        {
            var bytes = await api.Admin.GetByteArrayAsync($"{Base}/department-progress/export?format=csv");
            var text = Encoding.UTF8.GetString(bytes);
            Assert.Contains("'=CMD,\"'@evil,\"\"x\"\"\"", text, StringComparison.Ordinal);
        }
        finally
        {
            await api.Admin.DeleteAsync($"/api/v1/departments/{id}");
        }
    }

    [Theory]
    [InlineData("department-progress/export", HttpStatusCode.BadRequest, "validation_error")]
    [InlineData("department-progress/export?format=xml", HttpStatusCode.BadRequest, "validation_error")]
    [InlineData("project-summary/export?format=csv&departmentId=0", HttpStatusCode.BadRequest, "validation_error")]
    [InlineData("project-summary?departmentId=0", HttpStatusCode.BadRequest, "validation_error")]
    [InlineData("user-workload?departmentId=abc", HttpStatusCode.BadRequest, "validation_error")]
    [InlineData("unknown/export?format=csv", HttpStatusCode.NotFound, "not_found")]
    public async Task Invalid_requests_are_rejected(string path, HttpStatusCode status, string code)
    {
        var response = await api.Admin.GetAsync($"{Base}/{path}");
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("department-progress")]
    [InlineData("project-summary")]
    [InlineData("user-workload")]
    [InlineData("department-progress/export?format=csv")]
    public async Task Reports_require_manager_or_admin(string path)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous.GetAsync($"{Base}/{path}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Member.GetAsync($"{Base}/{path}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Manager.GetAsync($"{Base}/{path}")).StatusCode);
    }
}
