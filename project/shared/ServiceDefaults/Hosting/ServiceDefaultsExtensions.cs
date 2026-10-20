using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceDefaults.Hosting;

public static class ServiceDefaultsExtensions
{
    /// <summary>Standard problem-details error responses for every service.</summary>
    public static IServiceCollection AddServiceDefaults(this IServiceCollection services)
    {
        services.AddProblemDetails();
        return services;
    }

    /// <summary>Health endpoint polled by the Monitoring Service. Anonymous on purpose.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder app, string serviceName)
    {
        app.MapGet("/health", () => Results.Ok(new { service = serviceName, status = "Healthy" }))
           .AllowAnonymous();
        return app;
    }

    /// <summary>Shortcut for a single-field validation error.</summary>
    public static IResult ValidationError(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
