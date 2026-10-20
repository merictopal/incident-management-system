namespace IncidentService.Models;

/// <summary>Allowed incident statuses (lifecycle: Open → In Progress → Resolved → Closed).</summary>
public static class IncidentStatus
{
    public const string Open = "Open";
    public const string InProgress = "In Progress";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";

    public static readonly IReadOnlyList<string> All = [Open, InProgress, Resolved, Closed];

    public static string? Normalize(string? value) => AllowedValue.Match(All, value);
}

/// <summary>Allowed incident priorities.</summary>
public static class IncidentPriority
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";

    public static readonly IReadOnlyList<string> All = [Low, Medium, High, Critical];

    public static string? Normalize(string? value) => AllowedValue.Match(All, value);
}

internal static class AllowedValue
{
    /// <summary>Case-insensitive match against a fixed list; returns the canonical spelling.</summary>
    public static string? Match(IReadOnlyList<string> allowed, string? value) =>
        allowed.FirstOrDefault(item => string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
