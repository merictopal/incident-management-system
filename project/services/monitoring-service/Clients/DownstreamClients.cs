using ServiceDefaults.Contracts;

namespace MonitoringService.Clients;

/// <summary>Calls Incident Service's internal API to open and resolve automatic incidents.</summary>
public sealed class IncidentClient(HttpClient http)
{
    private sealed record CreatedIncident(Guid Id);

    public async Task<Guid?> CreateCriticalIncidentAsync(string serviceName, string error, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/internal/incidents", new
        {
            title = $"Service unavailable: {serviceName}",
            description = $"Monitoring Service detected that {serviceName} is unavailable. Details: {error}",
            priority = "Critical",
            source = "monitoring-service"
        }, ct);

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreatedIncident>(ct);
        return created?.Id;
    }

    public async Task ResolveIncidentAsync(Guid incidentId, string serviceName, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync($"/internal/incidents/{incidentId}/resolve", new
        {
            message = $"{serviceName} is healthy again. Resolved automatically by Monitoring Service.",
            source = "monitoring-service"
        }, ct);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>Pushes the monitoring snapshot to Notification Service, which forwards it to IT Support over SignalR.</summary>
public sealed class StatusPublisher(HttpClient http)
{
    public async Task PublishAsync(IReadOnlyList<ServiceStatus> snapshot, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/internal/service-status", snapshot, ct);
        response.EnsureSuccessStatusCode();
    }
}
