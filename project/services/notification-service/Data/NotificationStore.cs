using ServiceDefaults.Contracts;

namespace NotificationService.Data;

/// <summary>Keeps the most recent notifications in memory so a client can load history on login.</summary>
public sealed class NotificationStore
{
    private const int Capacity = 200;
    private readonly LinkedList<IncidentNotification> _items = new();
    private readonly object _gate = new();

    public void Add(IncidentNotification notification)
    {
        lock (_gate)
        {
            _items.AddFirst(notification);
            if (_items.Count > Capacity) _items.RemoveLast();
        }
    }

    public IReadOnlyList<IncidentNotification> Latest(int take, string? reporterId = null)
    {
        lock (_gate)
        {
            return _items
                .Where(item => reporterId is null || item.ReporterId == reporterId)
                .Take(take)
                .ToList();
        }
    }
}
