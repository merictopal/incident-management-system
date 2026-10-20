using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ServiceDefaults.Auth;

/// <summary>
/// Protects service-to-service endpoints (/internal/...). These routes are not
/// published by the API Gateway, and callers must also present a shared key.
/// </summary>
public static class InternalApiKey
{
    public const string HeaderName = "X-Internal-Api-Key";
    public const string ConfigKey = "Internal:ApiKey";
    public const string DevelopmentKey = "dev-only-internal-api-key";

    public static string GetInternalApiKey(this IConfiguration configuration, IHostEnvironment environment)
    {
        var key = configuration[ConfigKey];
        if (!string.IsNullOrWhiteSpace(key)) return key;
        if (environment.IsDevelopment()) return DevelopmentKey;
        throw new InvalidOperationException($"{ConfigKey} must be configured outside the Development environment.");
    }

    /// <summary>Adds an endpoint filter that rejects requests without the correct internal API key.</summary>
    public static TBuilder RequireInternalApiKey<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var services = context.HttpContext.RequestServices;
            var expected = services.GetRequiredService<IConfiguration>()
                .GetInternalApiKey(services.GetRequiredService<IHostEnvironment>());
            var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

            var valid = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided),
                Encoding.UTF8.GetBytes(expected));

            return valid ? await next(context) : Results.Unauthorized();
        });
        return builder;
    }
}
