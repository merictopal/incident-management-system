using System.Diagnostics;
using Microsoft.Extensions.Options;
using MonitoringService.Clients;
using MonitoringService.Options;
using MonitoringService.State;

namespace MonitoringService.Workers;

/// <summary>
/// Background loop: checks every monitored service in parallel, records the
/// result, opens/resolves automatic incidents on state changes and pushes the
/// live snapshot to IT Support through Notification Service.
/// </summary>
public sealed class MonitoringWorker(
    IHttpClientFactory httpClientFactory,
    IServiceProvider services,
    MonitoringState state,
    IOptions<MonitoringOptions> options,
    ILogger<MonitoringWorker> logger) : BackgroundService
{
    public const string HealthClientName = "health-checks";
    private const string IncidentServiceName = "incident-service";

    private readonly MonitoringOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));
        do
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // A bug in one cycle must never stop monitoring for good.
                logger.LogError(ex, "Monitoring cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        // Parallel: one slow service no longer delays the checks of the others.
        var results = await Task.WhenAll(_options.Targets.Select(target => CheckAsync(target.Key, target.Value, ct)));

        foreach (var (name, transition, error) in results)
        {
            if (transition == HealthTransition.BecameUnhealthy) await OnBecameUnhealthyAsync(name, error!, ct);
            if (transition == HealthTransition.Recovered) await OnRecoveredAsync(name, ct);
        }

        await TryPublishSnapshotAsync(ct);
    }

    private async Task<(string Name, HealthTransition Transition, string? Error)> CheckAsync(
        string name, string baseUrl, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient(HealthClientName);
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var response = await http.GetAsync($"{baseUrl.TrimEnd('/')}/health", ct);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            if (response.IsSuccessStatusCode) return (name, state.RecordSuccess(name, elapsed), null);

            var error = $"Health endpoint returned HTTP {(int)response.StatusCode}.";
            return (name, state.RecordFailure(name, error, elapsed), error);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var error = ex is TaskCanceledException ? $"No response within {_options.TimeoutSeconds} s." : ex.Message;
            logger.LogWarning("{Service} health check failed: {Error}", name, error);
            return (name, state.RecordFailure(name, error, elapsed), error);
        }
    }

    private async Task OnBecameUnhealthyAsync(string name, string error, CancellationToken ct)
    {
        logger.LogWarning("{Service} is now Unhealthy.", name);

        // Incident Service cannot receive an incident about itself being down.
        if (name.Equals(IncidentServiceName, StringComparison.OrdinalIgnoreCase) || !state.IsHealthy(IncidentServiceName))
        {
            logger.LogWarning("Automatic incident for {Service} skipped: Incident Service is unavailable.", name);
            return;
        }

        try
        {
            using var scope = services.CreateScope();
            var incidents = scope.ServiceProvider.GetRequiredService<IncidentClient>();
            if (await incidents.CreateCriticalIncidentAsync(name, error, ct) is { } id)
            {
                state.SetOpenIncident(name, id);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Could not create automatic incident for {Service}: {Error}", name, ex.Message);
        }
    }

    private async Task OnRecoveredAsync(string name, CancellationToken ct)
    {
        logger.LogInformation("{Service} recovered.", name);
        if (state.TakeOpenIncident(name) is not { } incidentId) return;

        try
        {
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IncidentClient>().ResolveIncidentAsync(incidentId, name, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Could not resolve automatic incident for {Service}: {Error}", name, ex.Message);
        }
    }

    private async Task TryPublishSnapshotAsync(CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<StatusPublisher>().PublishAsync(state.GetSnapshot(), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Notification Service down: the snapshot is still available via GET /api/monitoring/services.
            logger.LogDebug("Could not push monitoring snapshot: {Error}", ex.Message);
        }
    }
}
