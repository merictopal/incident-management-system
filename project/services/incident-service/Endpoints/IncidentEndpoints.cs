using System.Security.Claims;
using IncidentService.Contracts;
using IncidentService.Data;
using IncidentService.Models;
using IncidentService.Services;
using ServiceDefaults.Auth;
using ServiceDefaults.Contracts;
using static ServiceDefaults.Hosting.ServiceDefaultsExtensions;

namespace IncidentService.Endpoints;

public static class IncidentEndpoints
{
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 2000;
    public const int MaxMessageLength = 1000;

    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder app)
    {
        // Public API (reached through the API Gateway). Every route needs a valid JWT.
        var api = app.MapGroup("/api/incidents").RequireAuthorization();
        api.MapGet("/", GetIncidents);
        api.MapGet("/{id:guid}", GetIncident);
        api.MapPost("/", CreateIncident);
        api.MapPost("/{id:guid}/updates", AddUpdate);
        api.MapPatch("/{id:guid}/status", ChangeStatus).RequireAuthorization(Roles.ItSupportPolicy);
        api.MapDelete("/{id:guid}", DeleteIncident).RequireAuthorization(Roles.ItSupportPolicy);

        // Internal API for Monitoring Service. Not routed by the gateway + API key protected.
        var internalApi = app.MapGroup("/internal/incidents").RequireInternalApiKey();
        internalApi.MapPost("/", CreateSystemIncident);
        internalApi.MapPost("/{id:guid}/resolve", ResolveSystemIncident);

        return app;
    }

    /// <summary>IT Support sees every incident; an employee only ever sees their own.</summary>
    private static IResult GetIncidents(string? reporterId, string? status, ClaimsPrincipal user, IncidentStore store)
    {
        var effectiveReporter = user.IsItSupport() ? reporterId : user.GetUserId();
        var effectiveStatus = status is null ? null : IncidentStatus.Normalize(status);
        if (status is not null && effectiveStatus is null) return InvalidStatus();

        return Results.Ok(store.Query(effectiveReporter, effectiveStatus));
    }

    private static IResult GetIncident(Guid id, ClaimsPrincipal user, IncidentStore store) =>
        FindVisible(id, user, store) is { } incident ? Results.Ok(incident) : Results.NotFound();

    private static async Task<IResult> CreateIncident(
        CreateIncidentRequest request, ClaimsPrincipal user, IncidentStore store, NotificationPublisher publisher)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title)) return ValidationError("title", "Title is required.");
        if (title.Length > MaxTitleLength) return ValidationError("title", $"Title must be at most {MaxTitleLength} characters.");

        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length > MaxDescriptionLength)
            return ValidationError("description", $"Description must be at most {MaxDescriptionLength} characters.");

        var priority = request.Priority is null ? IncidentPriority.Medium : IncidentPriority.Normalize(request.Priority);
        if (priority is null) return ValidationError("priority", $"Priority must be one of: {string.Join(", ", IncidentPriority.All)}.");

        // Reporter identity comes from the signed token, never from the request body.
        var incident = new Incident(title, description, priority, user.GetUserId(), user.GetUserName());
        store.Add(incident);

        await publisher.PublishAsync(incident, IncidentEventTypes.Created, incident.ReporterId, incident.ReporterName,
            $"#{incident.ReporterId} {incident.ReporterName} reported \"{incident.Title}\".");

        return Results.Created($"/api/incidents/{incident.Id}", incident);
    }

    /// <summary>IT Support can comment on any incident; the reporter can reply on their own incident.</summary>
    private static async Task<IResult> AddUpdate(
        Guid id, AddUpdateRequest request, ClaimsPrincipal user, IncidentStore store, NotificationPublisher publisher)
    {
        if (FindVisible(id, user, store) is not { } incident) return Results.NotFound();

        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message)) return ValidationError("message", "Update message is required.");
        if (message.Length > MaxMessageLength) return ValidationError("message", $"Message must be at most {MaxMessageLength} characters.");

        var update = incident.AddUpdate(message, user.GetUserId(), user.GetUserName());

        await publisher.PublishAsync(incident, IncidentEventTypes.Updated, update.AuthorId, update.AuthorName,
            $"#{update.AuthorId} {update.AuthorName} added an update to \"{incident.Title}\".");

        return Results.Ok(incident);
    }

    private static async Task<IResult> ChangeStatus(
        Guid id, ChangeStatusRequest request, ClaimsPrincipal user, IncidentStore store, NotificationPublisher publisher)
    {
        if (store.Get(id) is not { } incident) return Results.NotFound();

        var status = IncidentStatus.Normalize(request.Status);
        if (status is null) return InvalidStatus();
        if (status == incident.Status) return Results.Ok(incident); // nothing changed, no event

        var update = incident.ChangeStatus(status, user.GetUserId(), user.GetUserName());

        await publisher.PublishAsync(incident, IncidentEventTypes.StatusChanged, update.AuthorId, update.AuthorName,
            $"#{update.AuthorId} {update.AuthorName} changed \"{incident.Title}\" to {status}.");

        return Results.Ok(incident);
    }

    private static async Task<IResult> DeleteIncident(
        Guid id, ClaimsPrincipal user, IncidentStore store, NotificationPublisher publisher)
    {
        if (!store.TryRemove(id, out var incident) || incident is null) return Results.NotFound();

        await publisher.PublishAsync(incident, IncidentEventTypes.Deleted, user.GetUserId(), user.GetUserName(),
            $"\"{incident.Title}\" was removed by #{user.GetUserId()} {user.GetUserName()}.");

        return Results.NoContent();
    }

    private static async Task<IResult> CreateSystemIncident(
        CreateSystemIncidentRequest request, IncidentStore store, NotificationPublisher publisher)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title)) return ValidationError("title", "Title is required.");

        var source = string.IsNullOrWhiteSpace(request.Source) ? "monitoring-service" : request.Source.Trim();
        var priority = IncidentPriority.Normalize(request.Priority) ?? IncidentPriority.Critical;
        var incident = new Incident(Truncate(title, MaxTitleLength), Truncate(request.Description ?? "", MaxDescriptionLength),
            priority, source, "Monitoring Service");
        store.Add(incident);

        await publisher.PublishAsync(incident, IncidentEventTypes.Created, source, "Monitoring Service",
            $"Monitoring Service reported \"{incident.Title}\".");

        return Results.Created($"/api/incidents/{incident.Id}", incident);
    }

    private static async Task<IResult> ResolveSystemIncident(
        Guid id, ResolveSystemIncidentRequest request, IncidentStore store, NotificationPublisher publisher)
    {
        if (store.Get(id) is not { } incident) return Results.NotFound();

        var source = string.IsNullOrWhiteSpace(request.Source) ? "monitoring-service" : request.Source.Trim();
        incident.AddUpdate(Truncate(request.Message ?? "Service recovered.", MaxMessageLength), source, "Monitoring Service");
        if (incident.Status is IncidentStatus.Open or IncidentStatus.InProgress)
        {
            incident.ChangeStatus(IncidentStatus.Resolved, source, "Monitoring Service");
        }

        await publisher.PublishAsync(incident, IncidentEventTypes.StatusChanged, source, "Monitoring Service",
            $"Monitoring Service marked \"{incident.Title}\" as {incident.Status}.");

        return Results.Ok(incident);
    }

    /// <summary>Returns the incident only if the caller may see it (IT Support or the reporter).</summary>
    private static Incident? FindVisible(Guid id, ClaimsPrincipal user, IncidentStore store)
    {
        var incident = store.Get(id);
        if (incident is null) return null;
        return user.IsItSupport() || incident.ReporterId == user.GetUserId() ? incident : null;
    }

    private static IResult InvalidStatus() =>
        ValidationError("status", $"Status must be one of: {string.Join(", ", IncidentStatus.All)}.");

    private static string Truncate(string value, int max) => value.Length <= max ? value.Trim() : value[..max].Trim();
}
