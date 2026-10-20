using IncidentService.Data;
using IncidentService.Endpoints;
using IncidentService.Services;
using ServiceDefaults.Auth;
using ServiceDefaults.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults();
builder.AddJwtAuthentication();
builder.Services.AddSingleton<IncidentStore>();

// Typed HttpClient for Notification Service with a short timeout, so a slow
// notification service can never block incident requests for long.
var internalApiKey = builder.Configuration.GetInternalApiKey(builder.Environment);
builder.Services.AddHttpClient<NotificationPublisher>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:NotificationService:BaseUrl"] ?? "http://localhost:5003");
    client.Timeout = TimeSpan.FromSeconds(3);
    client.DefaultRequestHeaders.Add(InternalApiKey.HeaderName, internalApiKey);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoint("incident-service");
app.MapIncidentEndpoints();

app.Run();

// Exposed for integration tests (WebApplicationFactory<Program>).
public partial class Program { }
