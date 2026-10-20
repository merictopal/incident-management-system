namespace ServiceDefaults.Auth;

/// <summary>JWT settings, bound from the "Jwt" configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Development-only key. Docker Compose and production deployments must
    /// override it through the Jwt__SigningKey environment variable.
    /// </summary>
    public const string DevelopmentSigningKey = "dev-only-signing-key-change-me-0123456789abcdef";

    public string Issuer { get; set; } = "incident-management";
    public string Audience { get; set; } = "incident-management-client";
    public string SigningKey { get; set; } = string.Empty;
    public int LifetimeHours { get; set; } = 8;
}
