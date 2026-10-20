using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TestCommon;

namespace UserService.Tests;

public sealed class LoginTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record UserDto(int Id, string Name, string Role);
    private sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User, bool IsNewUser);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<HttpResponseMessage> Login(string name, string password, string role) =>
        await factory.CreateClient().PostAsJsonAsync("/api/users/login", new { name, password, role });

    [Fact]
    public async Task First_login_registers_user_and_returns_token()
    {
        var response = await Login(Unique("meric"), "secret1", "Employee");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.True(body.IsNewUser);
        Assert.True(body.User.Id > 1000);
        Assert.Equal("Employee", body.User.Role);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
    }

    [Fact]
    public async Task Same_name_and_password_returns_same_id_case_insensitive()
    {
        var name = Unique("ahmet");
        var first = await (await Login(name, "secret1", "Employee")).Content.ReadFromJsonAsync<LoginResponse>();
        var second = await Login(name.ToUpperInvariant(), "secret1", "employee");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal(first!.User.Id, body!.User.Id);
        Assert.False(body.IsNewUser);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var name = Unique("ayse");
        await Login(name, "correct-password", "Employee");

        var response = await Login(name, "wrong-password", "Employee");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Same_name_in_other_role_is_a_separate_identity()
    {
        var name = Unique("alex");
        var employee = await (await Login(name, "secret1", "Employee")).Content.ReadFromJsonAsync<LoginResponse>();
        var it = await (await Login(name, "secret1", "IT Support")).Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotEqual(employee!.User.Id, it!.User.Id);
        Assert.Equal("IT Support", it.User.Role);
    }

    [Theory]
    [InlineData("", "secret1", "Employee")]      // missing name
    [InlineData("Meric", "123", "Employee")]     // password too short
    [InlineData("Meric", "secret1", "Admin")]    // unknown role
    public async Task Invalid_input_returns_400(string name, string password, string role)
    {
        var response = await Login(name, password, role);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Password_hash_is_never_returned()
    {
        var response = await Login(Unique("zeynep"), "secret1", "Employee");
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Listing_users_requires_it_support()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/users");
        var employee = await factory.CreateClient().WithToken(TestTokens.Employee()).GetAsync("/api/users");
        var it = await factory.CreateClient().WithToken(TestTokens.ItSupport()).GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, employee.StatusCode);
        Assert.Equal(HttpStatusCode.OK, it.StatusCode);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var expired = TestTokens.Create("1001", "Meric", "IT Support", DateTime.UtcNow.AddMinutes(-10));
        var response = await factory.CreateClient().WithToken(expired).GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
