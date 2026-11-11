using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Data;
using NotificationService.Hubs;
using ServiceDefaults.Auth;
using ServiceDefaults.Contracts;

namespace NotificationService.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        // Notification history for the logged-in user (same visibility rules as the hub).
        app.MapGet("/api/notifications", (int? take, ClaimsPrincipal user, NotificationStore store) =>
        {
            var count = Math.Clamp(take ?? 30, 1, 100);
            var reporterFilter = user.IsItSupport() ? null : user.GetUserId();
            return Results.Ok(store.Latest(count, reporterFilter));
        }).RequireAuthorization();

        var internalApi = app.MapGroup("/internal").RequireInternalApiKey();

        // Called by Incident Service after every incident change.
        internalApi.MapPost("/notifications", async (
            IncidentNotification notification, NotificationStore store, IHubContext<NotificationHub> hub) =>
        {
            store.Add(notification);
            await hub.Clients
                .Groups(NotificationHub.ItSupportGroup, NotificationHub.UserGroup(notification.ReporterId))
                .SendAsync(HubEvents.IncidentChanged, notification);
            return Results.Accepted();
        });

        // Called by Monitoring Service after every health-check cycle (IT Support only).
        internalApi.MapPost("/service-status", async (
            IReadOnlyList<ServiceStatus> statuses, IHubContext<NotificationHub> hub) =>
        {
            await hub.Clients.Group(NotificationHub.ItSupportGroup)
                .SendAsync(HubEvents.ServiceStatusUpdated, statuses);
            return Results.Accepted();
        });

        return app;
    }
}
