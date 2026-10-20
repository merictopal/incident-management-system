using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ServiceDefaults.Auth;
using TestCommon;

namespace IncidentService.Tests;

public sealed class IncidentApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record UpdateDto(string Message, string AuthorId, string AuthorName);
    private sealed record IncidentDto(Guid Id, string Title, string Priority, string Status, string ReporterId, string ReporterName, List<UpdateDto> Updates);

    private readonly WebApplicationFactory<Program> _factory;

    public IncidentApiTests(WebApplicationFactory<Program> factory)
    {
        // Point the notification client at a closed port: publishing fails fast and must not break requests.
        _factory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Services:NotificationService:BaseUrl", "http://127.0.0.1:9"));
    }

    private HttpClient As(string token) => _factory.CreateClient().WithToken(token);

    private static string NewEmployee(out string id)
    {
        id = Random.Shared.Next(100_000, 999_999).ToString();
        return TestTokens.Employee(id, $"Employee {id}");
    }

    private static async Task<IncidentDto> Create(HttpClient client, string title = "VPN down", string priority = "High")
    {
        var response = await client.PostAsJsonAsync("/api/incidents", new { title, description = "details", priority });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IncidentDto>())!;
    }

    [Fact]
    public async Task Requests_without_token_are_rejected()
    {
        var response = await _factory.CreateClient().GetAsync("/api/incidents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reporter_comes_from_token_not_from_request_body()
    {
        var token = NewEmployee(out var id);
        var response = await As(token).PostAsJsonAsync("/api/incidents",
            new { title = "Printer", priority = "Low", reporterId = "9999", reporterName = "Someone Else" });

        var incident = await response.Content.ReadFromJsonAsync<IncidentDto>();
        Assert.Equal(id, incident!.ReporterId);
        Assert.Equal("Open", incident.Status);
    }

    [Fact]
    public async Task Employee_only_sees_own_incidents()
    {
        var a = As(NewEmployee(out _));
        var b = As(NewEmployee(out var bId));
        var incidentOfA = await Create(a);

        var listOfB = await b.GetFromJsonAsync<List<IncidentDto>>($"/api/incidents?reporterId={incidentOfA.ReporterId}");
        var directAccess = await b.GetAsync($"/api/incidents/{incidentOfA.Id}");

        Assert.DoesNotContain(listOfB!, incident => incident.Id == incidentOfA.Id);
        Assert.All(listOfB!, incident => Assert.Equal(bId, incident.ReporterId));
        Assert.Equal(HttpStatusCode.NotFound, directAccess.StatusCode);
    }

    [Fact]
    public async Task It_support_sees_all_incidents()
    {
        var incident = await Create(As(NewEmployee(out _)));
        var all = await As(TestTokens.ItSupport()).GetFromJsonAsync<List<IncidentDto>>("/api/incidents");
        Assert.Contains(all!, item => item.Id == incident.Id);
    }

    [Fact]
    public async Task Only_it_support_can_change_status()
    {
        var incident = await Create(As(NewEmployee(out _)));

        var byEmployee = await As(NewEmployee(out _)).PatchAsJsonAsync($"/api/incidents/{incident.Id}/status", new { status = "Resolved" });
        var byIt = await As(TestTokens.ItSupport("1002", "Alex")).PatchAsJsonAsync($"/api/incidents/{incident.Id}/status", new { status = "in progress" });

        Assert.Equal(HttpStatusCode.Forbidden, byEmployee.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byIt.StatusCode);
        var updated = await byIt.Content.ReadFromJsonAsync<IncidentDto>();
        Assert.Equal("In Progress", updated!.Status);
        Assert.Equal("1002", updated.Updates.Single().AuthorId);
    }

    [Theory]
    [InlineData("Banana")]
    [InlineData("")]
    public async Task Invalid_status_returns_400(string status)
    {
        var incident = await Create(As(NewEmployee(out _)));
        var response = await As(TestTokens.ItSupport()).PatchAsJsonAsync($"/api/incidents/{incident.Id}/status", new { status });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_priority_and_too_long_title_return_400()
    {
        var client = As(NewEmployee(out _));
        var badPriority = await client.PostAsJsonAsync("/api/incidents", new { title = "x", priority = "Urgent!!" });
        var longTitle = await client.PostAsJsonAsync("/api/incidents", new { title = new string('a', 500) });

        Assert.Equal(HttpStatusCode.BadRequest, badPriority.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longTitle.StatusCode);
    }

    [Fact]
    public async Task Reporter_can_reply_but_others_cannot()
    {
        var owner = As(NewEmployee(out _));
        var incident = await Create(owner);

        var ownReply = await owner.PostAsJsonAsync($"/api/incidents/{incident.Id}/updates", new { message = "More info" });
        var strangerReply = await As(NewEmployee(out _)).PostAsJsonAsync($"/api/incidents/{incident.Id}/updates", new { message = "Hi" });

        Assert.Equal(HttpStatusCode.OK, ownReply.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, strangerReply.StatusCode);
    }

    [Fact]
    public async Task Only_it_support_can_delete()
    {
        var owner = As(NewEmployee(out _));
        var incident = await Create(owner);

        var byOwner = await owner.DeleteAsync($"/api/incidents/{incident.Id}");
        var byIt = await As(TestTokens.ItSupport()).DeleteAsync($"/api/incidents/{incident.Id}");
        var afterDelete = await As(TestTokens.ItSupport()).GetAsync($"/api/incidents/{incident.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, byOwner.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byIt.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Internal_endpoint_requires_api_key()
    {
        var body = new { title = "Service unavailable: user-service", priority = "Critical" };

        var withoutKey = await _factory.CreateClient().PostAsJsonAsync("/internal/incidents", body);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(InternalApiKey.HeaderName, InternalApiKey.DevelopmentKey);
        var withKey = await client.PostAsJsonAsync("/internal/incidents", body);

        Assert.Equal(HttpStatusCode.Unauthorized, withoutKey.StatusCode);
        Assert.Equal(HttpStatusCode.Created, withKey.StatusCode);
    }

    [Fact]
    public async Task Concurrent_updates_are_all_kept()
    {
        var owner = As(NewEmployee(out _));
        var incident = await Create(owner);
        var it = As(TestTokens.ItSupport());

        await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            it.PostAsJsonAsync($"/api/incidents/{incident.Id}/updates", new { message = $"update {i}" })));

        var result = await it.GetFromJsonAsync<IncidentDto>($"/api/incidents/{incident.Id}");
        Assert.Equal(50, result!.Updates.Count);
    }
}
