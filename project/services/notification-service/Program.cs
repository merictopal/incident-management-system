using NotificationService.Data;
using NotificationService.Endpoints;
using NotificationService.Hubs;
using ServiceDefaults.Auth;
using ServiceDefaults.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults();
builder.AddJwtAuthentication();
builder.Services.AddSignalR();
builder.Services.AddSingleton<NotificationStore>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoint("notification-service");
app.MapNotificationEndpoints();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
