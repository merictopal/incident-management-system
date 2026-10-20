namespace ServiceDefaults.Contracts;

/// <summary>Sent by Incident Service to Notification Service whenever an incident changes.</summary>
public sealed record IncidentNotification(
    string Type,
    Guid IncidentId,
    string Title,
    string Priority,
    string Status,
    string ReporterId,
    string ReporterName,
    string ActorId,
    string ActorName,
    string Message,
    DateTimeOffset OccurredAt);

/// <summary>Health information for one monitored service.</summary>
public sealed record ServiceStatus(
    string Name,
    string Status,
    DateTimeOffset LastCheckedAt,
    double ResponseTimeMs,
    int ConsecutiveFailures,
    string? Error);

/// <summary>Incident types published through SignalR.</summary>
public static class IncidentEventTypes
{
    public const string Created = "Created";
    public const string StatusChanged = "StatusChanged";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
}

/// <summary>Names of SignalR client methods, shared so server and docs stay in sync.</summary>
public static class HubEvents
{
    public const string IncidentChanged = "IncidentChanged";
    public const string ServiceStatusUpdated = "ServiceStatusUpdated";
}
