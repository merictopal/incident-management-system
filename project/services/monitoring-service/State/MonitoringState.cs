using MonitoringService.Options;
using ServiceDefaults.Contracts;

namespace MonitoringService.State;

public enum HealthTransition { None, BecameUnhealthy, Recovered }

/// <summary>Thread-safe store of the latest health status of each monitored service.</summary>
public sealed class MonitoringState
{
    public const string Unknown = "Unknown";
    public const string Healthy = "Healthy";
    public const string Unhealthy = "Unhealthy";

    private readonly Dictionary<string, ServiceStatus> _services = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _openIncidents = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly int _failureThreshold;

    public MonitoringState(Microsoft.Extensions.Options.IOptions<MonitoringOptions> options)
    {
        _failureThreshold = Math.Max(1, options.Value.FailureThreshold);
        foreach (var name in options.Value.Targets.Keys)
        {
            _services[name] = new ServiceStatus(name, Unknown, DateTimeOffset.UtcNow, 0, 0, null);
        }
    }

    public HealthTransition RecordSuccess(string name, double responseTimeMs)
    {
        lock (_gate)
        {
            var wasUnhealthy = _services.TryGetValue(name, out var old) && old.Status == Unhealthy;
            _services[name] = new ServiceStatus(name, Healthy, DateTimeOffset.UtcNow, Math.Round(responseTimeMs, 1), 0, null);
            return wasUnhealthy ? HealthTransition.Recovered : HealthTransition.None;
        }
    }

    public HealthTransition RecordFailure(string name, string error, double responseTimeMs)
    {
        lock (_gate)
        {
            _services.TryGetValue(name, out var old);
            var failures = (old?.ConsecutiveFailures ?? 0) + 1;
            var reachedThreshold = failures >= _failureThreshold;

            // Below the threshold the previous status is kept, only the error is recorded.
            var status = reachedThreshold ? Unhealthy : old?.Status ?? Unknown;
            _services[name] = new ServiceStatus(name, status, DateTimeOffset.UtcNow, Math.Round(responseTimeMs, 1), failures, error);

            return reachedThreshold && old?.Status != Unhealthy
                ? HealthTransition.BecameUnhealthy
                : HealthTransition.None;
        }
    }

    public bool IsHealthy(string name)
    {
        lock (_gate) return _services.TryGetValue(name, out var status) && status.Status == Healthy;
    }

    public void SetOpenIncident(string name, Guid incidentId)
    {
        lock (_gate) _openIncidents[name] = incidentId;
    }

    public Guid? TakeOpenIncident(string name)
    {
        lock (_gate) return _openIncidents.Remove(name, out var id) ? id : null;
    }

    public IReadOnlyList<ServiceStatus> GetSnapshot()
    {
        lock (_gate) return _services.Values.OrderBy(service => service.Name).ToList();
    }
}
