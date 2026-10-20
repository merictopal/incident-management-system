using System.Collections.Concurrent;
using IncidentService.Models;

namespace IncidentService.Data;

/// <summary>In-memory incident storage (one store per service, no shared database).</summary>
public sealed class IncidentStore
{
    private readonly ConcurrentDictionary<Guid, Incident> _incidents = new();

    public IEnumerable<Incident> Query(string? reporterId = null, string? status = null) =>
        _incidents.Values
            .Where(incident => reporterId is null || incident.ReporterId == reporterId)
            .Where(incident => status is null || incident.Status == status)
            .OrderByDescending(incident => incident.CreatedAt);

    public Incident? Get(Guid id) => _incidents.GetValueOrDefault(id);

    public void Add(Incident incident) => _incidents[incident.Id] = incident;

    public bool TryRemove(Guid id, out Incident? incident) => _incidents.TryRemove(id, out incident);
}
