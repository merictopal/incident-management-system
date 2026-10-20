using IncidentService.Models;
using ServiceDefaults.Contracts;

namespace IncidentService.Services;

/// <summary>
/// Sends incident events to Notification Service. Failures are logged but never
/// break the incident operation itself: the incident is already saved.
/// </summary>
public sealed class NotificationPublisher(HttpClient http, ILogger<NotificationPublisher> logger)
{
    public async Task PublishAsync(Incident incident, string type, string actorId, string actorName, string message)
    {
        var notification = new IncidentNotification(
            type, incident.Id, incident.Title, incident.Priority, incident.Status,
            incident.ReporterId, incident.ReporterName, actorId, actorName, message, DateTimeOffset.UtcNow);

        try
        {
            using var response = await http.PostAsJsonAsync("/internal/notifications", notification);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Notification Service rejected {Type} event for {IncidentId}: HTTP {Status}",
                    type, incident.Id, (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // TaskCanceledException = the 3-second timeout configured in Program.cs.
            logger.LogWarning("Notification Service unavailable, {Type} event for {IncidentId} was not delivered: {Error}",
                type, incident.Id, ex.Message);
        }
    }
}
