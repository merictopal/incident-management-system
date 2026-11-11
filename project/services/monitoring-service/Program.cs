using MonitoringService.Clients;
using MonitoringService.Options;
using MonitoringService.State;
using MonitoringService.Workers;
using ServiceDefaults.Auth;
using ServiceDefaults.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults();
builder.AddJwtAuthentication();

var config = builder.Configuration;
string ServiceUrl(string key, string fallback) => config[$"Services:{key}:BaseUrl"] ?? fallback;

var userServiceUrl = ServiceUrl("UserService", "http://localhost:5001");
var incidentServiceUrl = ServiceUrl("IncidentService", "http://localhost:5002");
var notificationServiceUrl = ServiceUrl("NotificationService", "http://localhost:5003");

builder.Services.Configure<MonitoringOptions>(config.GetSection(MonitoringOptions.SectionName));
builder.Services.PostConfigure<MonitoringOptions>(options =>
{
    if (options.Targets.Count > 0) return;
    options.Targets["user-service"] = userServiceUrl;
    options.Targets["incident-service"] = incidentServiceUrl;
    options.Targets["notification-service"] = notificationServiceUrl;
});

var timeout = TimeSpan.FromSeconds(config.GetValue(MonitoringOptions.SectionName + ":TimeoutSeconds", 3));
var internalApiKey = config.GetInternalApiKey(builder.Environment);

builder.Services.AddHttpClient(MonitoringWorker.HealthClientName, client => client.Timeout = timeout);
builder.Services.AddHttpClient<IncidentClient>(client =>
{
    client.BaseAddress = new Uri(incidentServiceUrl);
    client.Timeout = timeout;
    client.DefaultRequestHeaders.Add(InternalApiKey.HeaderName, internalApiKey);
});
builder.Services.AddHttpClient<StatusPublisher>(client =>
{
    client.BaseAddress = new Uri(notificationServiceUrl);
    client.Timeout = timeout;
    client.DefaultRequestHeaders.Add(InternalApiKey.HeaderName, internalApiKey);
});

builder.Services.AddSingleton<MonitoringState>();
builder.Services.AddHostedService<MonitoringWorker>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoint("monitoring-service");
app.MapGet("/api/monitoring/services", (MonitoringState state) => Results.Ok(state.GetSnapshot()))
   .RequireAuthorization(Roles.ItSupportPolicy);

app.Run();
