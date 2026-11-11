namespace MonitoringService.Options;

/// <summary>Bound from the "Monitoring" configuration section.</summary>
public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>Time between two health-check cycles.</summary>
    public int IntervalSeconds { get; set; } = 5;

    /// <summary>A health check that takes longer than this counts as a failure.</summary>
    public int TimeoutSeconds { get; set; } = 3;

    /// <summary>
    /// Consecutive failed checks before a service is marked Unhealthy. Prevents
    /// false alarms from a single slow response or from services still starting up.
    /// </summary>
    public int FailureThreshold { get; set; } = 2;

    /// <summary>Monitored services: name → base URL (configured via Services:{Name}:BaseUrl).</summary>
    public Dictionary<string, string> Targets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
