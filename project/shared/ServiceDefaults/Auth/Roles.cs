namespace ServiceDefaults.Auth;

/// <summary>The two roles supported by the system.</summary>
public static class Roles
{
    public const string Employee = "Employee";
    public const string ItSupport = "IT Support";

    /// <summary>Authorization policy name that only allows IT Support users.</summary>
    public const string ItSupportPolicy = "ItSupportOnly";

    /// <summary>Converts user input such as "it support" or "ITStaff" to a canonical role name.</summary>
    public static string? Normalize(string? role)
    {
        if (string.Equals(role, Employee, StringComparison.OrdinalIgnoreCase)) return Employee;
        if (string.Equals(role, ItSupport, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "ITStaff", StringComparison.OrdinalIgnoreCase)) return ItSupport;
        return null;
    }
}
