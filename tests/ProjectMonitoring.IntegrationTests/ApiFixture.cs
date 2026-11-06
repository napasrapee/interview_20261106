using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ProjectMonitoring.IntegrationTests;

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
