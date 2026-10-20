using System.Security.Claims;

namespace ServiceDefaults.Auth;

/// <summary>Short JWT claim names used across the services.</summary>
public static class AppClaims
{
    public const string UserId = "sub";
    public const string Name = "name";
    public const string Role = "role";
}

public static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(AppClaims.UserId) ?? throw new InvalidOperationException("Token has no user id.");

    public static string GetUserName(this ClaimsPrincipal user) =>
        user.FindFirstValue(AppClaims.Name) ?? "Unknown";

    public static bool IsItSupport(this ClaimsPrincipal user) => user.IsInRole(Roles.ItSupport);
}
