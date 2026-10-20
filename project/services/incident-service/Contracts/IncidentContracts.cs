namespace IncidentService.Contracts;

public sealed record CreateIncidentRequest(string? Title, string? Description, string? Priority);
public sealed record ChangeStatusRequest(string? Status);
public sealed record AddUpdateRequest(string? Message);

/// <summary>Used only by Monitoring Service through the internal endpoint.</summary>
public sealed record CreateSystemIncidentRequest(string? Title, string? Description, string? Priority, string? Source);
public sealed record ResolveSystemIncidentRequest(string? Message, string? Source);
