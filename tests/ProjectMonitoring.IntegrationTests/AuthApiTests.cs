using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjectMonitoring.Api;
using ProjectMonitoring.Application.Abstractions;

namespace ProjectMonitoring.IntegrationTests;

/// <summary>Role-protected endpoints that exist only in tests, until the real ones are added.</summary>
[ApiController]
[Route("test")]
public sealed class PolicyProbeController : ControllerBase
{
    [HttpGet("admin")]
    [Authorize(Policy = Policies.AdminOnly)]
    public IActionResult Admin() => Ok();

    [HttpGet("manager")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public IActionResult Manager() => Ok();

    [HttpGet("boom")]
    public IActionResult Boom() => throw new InvalidOperationException("secret internal detail");

    [HttpGet("database-outage")]
    public IActionResult DatabaseOutage() =>
        throw new InvalidOperationException("wrapper", new FakeDbException(isTransient: true));

    [HttpGet("database-error")]
    public IActionResult DatabaseError() =>
        throw new InvalidOperationException("wrapper", new FakeDbException(isTransient: false));
}

public sealed class FakeDbException(bool isTransient) : System.Data.Common.DbException("secret database detail")
{
    public override bool IsTransient { get; } = isTransient;
}

public class AuthApiTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private const string AdminEmail = ApiFactory.AdminEmail;
    private const string AdminPassword = ApiFactory.AdminPassword;
    private const string Secret = ApiFactory.Secret;
    private const string UserPassword = "user-password-1";

    private WebApplicationFactory<Program> Factory(int loginLimit = 100_000) =>
        ApiFactory.Create(fixture.ConnectionString, loginLimit, typeof(PolicyProbeController).Assembly);

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private static async Task SetPasswordAsync(WebApplicationFactory<Program> factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .FindByEmailAsync(email, CancellationToken.None);
        user!.SetPasswordHash(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(UserPassword));
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<HttpStatusCode> GetAsync(HttpClient client, string path, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return (await client.SendAsync(request)).StatusCode;
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.False(body.TryGetProperty("stackTrace", out _));
        return body;
    }

    private static string CraftToken(string secret, DateTime expires, string issuer = "test-issuer") =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = "test-audience",
            NotBefore = expires.AddHours(-2),
            IssuedAt = expires.AddHours(-2),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity([new Claim("sub", "1"), new Claim("role", "admin")]),
        });

    [Fact]
    public async Task Bootstrapped_admin_can_log_in_and_read_profile_without_hash()
    {
        using var factory = Factory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, AdminEmail, AdminPassword);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/v1/auth/me");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal(AdminEmail, body.GetProperty("email").GetString());
        Assert.Equal("admin", body.GetProperty("role").GetString());
        Assert.DoesNotContain("hash", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bootstrap_is_repeatable_and_login_ignores_email_case()
    {
        using (var first = Factory())
        {
            first.CreateClient();
        }

        using var second = Factory();
        var client = second.CreateClient();
        Assert.NotEmpty(await LoginAsync(client, AdminEmail.ToUpperInvariant(), AdminPassword));
    }

    [Theory]
    [InlineData(AdminEmail, "wrong-password")]
    [InlineData("nobody@example.com", "wrong-password")]
    [InlineData("somchai@example.com", "any-password")] // seeded account has no password
    public async Task Failed_logins_share_one_response(string email, string password)
    {
        using var factory = Factory();
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        var body = await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "invalid_credentials");
        Assert.Equal("Email or password is incorrect.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Invalid_login_body_is_400()
    {
        using var factory = Factory();
        var client = factory.CreateClient();

        var invalid = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nope", password = "x" });
        var body = await AssertProblemAsync(invalid, HttpStatusCode.BadRequest, "validation_error");
        Assert.True(body.GetProperty("errors").TryGetProperty("email", out _));

        var malformed = await client.PostAsync(
            "/api/v1/auth/login", new StringContent("{not json", Encoding.UTF8, "application/json"));
        await AssertProblemAsync(malformed, HttpStatusCode.BadRequest, "malformed_request");
    }

    [Fact]
    public async Task Token_problems_are_401()
    {
        using var factory = Factory();
        var client = factory.CreateClient();
        var future = DateTime.UtcNow.AddHours(1);

        await AssertProblemAsync(await client.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "unauthorized");
        Assert.Equal(HttpStatusCode.Unauthorized, await GetAsync(client, "/api/v1/auth/me", "not-a-token"));
        Assert.Equal(HttpStatusCode.Unauthorized, await GetAsync(
            client, "/api/v1/auth/me", CraftToken("another-secret-another-secret-another-secret", future)));
        Assert.Equal(HttpStatusCode.Unauthorized, await GetAsync(
            client, "/api/v1/auth/me", CraftToken(Secret, future, issuer: "other-issuer")));
        Assert.Equal(HttpStatusCode.Unauthorized, await GetAsync(
            client, "/api/v1/auth/me", CraftToken(Secret, DateTime.UtcNow.AddMinutes(-5))));
    }

    [Fact]
    public async Task Roles_are_enforced_with_403()
    {
        using var factory = Factory();
        await SetPasswordAsync(factory, "wipa@example.com");
        await SetPasswordAsync(factory, "somchai@example.com");
        var client = factory.CreateClient();
        var member = await LoginAsync(client, "wipa@example.com", UserPassword);
        var manager = await LoginAsync(client, "somchai@example.com", UserPassword);
        var admin = await LoginAsync(client, AdminEmail, AdminPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, await GetAsync(client, "/test/manager", null));
        Assert.Equal(HttpStatusCode.Forbidden, await GetAsync(client, "/test/manager", member));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, "/test/manager", manager));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, "/test/manager", admin));
        Assert.Equal(HttpStatusCode.Forbidden, await GetAsync(client, "/test/admin", manager));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, "/test/admin", admin));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/test/admin");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", member);
        await AssertProblemAsync(await client.SendAsync(request), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Unexpected_error_is_500_without_internal_details()
    {
        using var factory = Factory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsync(client, AdminEmail, AdminPassword));

        var response = await client.GetAsync("/test/boom");

        var body = await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "internal_error");
        Assert.DoesNotContain("secret internal detail", body.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_rate_limit_returns_429()
    {
        using var factory = Factory(loginLimit: 3);
        var client = factory.CreateClient();
        var credentials = new { email = "nobody@example.com", password = "wrong-password" };

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/api/v1/auth/login", credentials)).StatusCode);
        }

        await AssertProblemAsync(
            await client.PostAsJsonAsync("/api/v1/auth/login", credentials),
            HttpStatusCode.TooManyRequests,
            "rate_limited");
    }

    [Fact]
    public async Task Health_is_public()
    {
        using var factory = Factory();
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/health")).StatusCode);
    }
}
