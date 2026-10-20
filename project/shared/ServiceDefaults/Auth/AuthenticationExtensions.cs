using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace ServiceDefaults.Auth;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication and the role-based policies.
    /// Every service validates tokens itself (defense in depth), so a request
    /// that bypasses the API Gateway is still rejected without a valid token.
    /// </summary>
    public static IHostApplicationBuilder AddJwtAuthentication(this IHostApplicationBuilder builder)
    {
        var jwt = builder.GetJwtOptions();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // keep "sub", "name", "role" as-is
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = CreateSigningKey(jwt.SigningKey),
                    NameClaimType = AppClaims.Name,
                    RoleClaimType = AppClaims.Role,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // Browsers cannot send headers on WebSocket requests, so SignalR
                // sends the token as ?access_token=... for hub connections.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Roles.ItSupportPolicy, policy => policy.RequireRole(Roles.ItSupport));

        return builder;
    }

    /// <summary>Reads and validates the JWT options (fails fast outside Development when no key is set).</summary>
    public static JwtOptions GetJwtOptions(this IHostApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            if (!builder.Environment.IsDevelopment())
            {
                throw new InvalidOperationException("Jwt:SigningKey must be configured outside the Development environment.");
            }
            options.SigningKey = JwtOptions.DevelopmentSigningKey;
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes long for HMAC-SHA256.");
        }

        return options;
    }

    public static SymmetricSecurityKey CreateSigningKey(string key) => new(Encoding.UTF8.GetBytes(key));
}
