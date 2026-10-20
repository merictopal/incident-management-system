using ServiceDefaults.Auth;
using ServiceDefaults.Hosting;
using UserService.Data;
using UserService.Endpoints;
using UserService.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults();
builder.AddJwtAuthentication();
builder.Services.AddSingleton(builder.GetJwtOptions());
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<UserStore>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoint("user-service");
app.MapUserEndpoints();

app.Run();

// Exposed for integration tests (WebApplicationFactory<Program>).
public partial class Program { }
