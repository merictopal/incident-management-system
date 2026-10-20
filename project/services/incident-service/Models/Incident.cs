namespace IncidentService.Models;

public sealed record IncidentUpdate(Guid Id, string Message, string AuthorId, string AuthorName, DateTimeOffset CreatedAt);

/// <summary>
/// Incident aggregate. Mutations go through methods guarded by a lock, because
/// several requests can modify the same incident at the same time.
/// </summary>
public sealed class Incident
{
    private readonly object _gate = new();
    private readonly List<IncidentUpdate> _updates = [];

    public Incident(string title, string description, string priority, string reporterId, string reporterName)
    {
        Id = Guid.NewGuid();
        Title = title;
        Description = description;
        Priority = priority;
        Status = IncidentStatus.Open;
        ReporterId = reporterId;
        ReporterName = reporterName;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string Priority { get; }
    public string Status { get; private set; }
    public string ReporterId { get; }
    public string ReporterName { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Snapshot copy, safe to serialize while other requests add updates.</summary>
    public IReadOnlyList<IncidentUpdate> Updates
    {
        get { lock (_gate) return _updates.ToArray(); }
    }

    public IncidentUpdate ChangeStatus(string status, string authorId, string authorName)
    {
        lock (_gate)
        {
            var previous = Status;
            Status = status;
            return AddUpdateLocked($"Status changed from {previous} to {status}.", authorId, authorName);
        }
    }

    public IncidentUpdate AddUpdate(string message, string authorId, string authorName)
    {
        lock (_gate) return AddUpdateLocked(message, authorId, authorName);
    }

    private IncidentUpdate AddUpdateLocked(string message, string authorId, string authorName)
    {
        var update = new IncidentUpdate(Guid.NewGuid(), message, authorId, authorName, DateTimeOffset.UtcNow);
        _updates.Add(update);
        UpdatedAt = update.CreatedAt;
        return update;
    }
}
