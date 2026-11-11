<script lang="ts">
  import type { ServiceStatus } from '$lib/types';

  let { statuses }: { statuses: ServiceStatus[] } = $props();

  function detail(service: ServiceStatus): string {
    if (service.status === 'Healthy') return `${service.responseTimeMs} ms response`;
    if (service.status === 'Unhealthy') return service.error ?? 'Health check failed';
    if (service.consecutiveFailures > 0) return `Not responding (${service.consecutiveFailures} failed check)`;
    return 'Waiting for first health check';
  }
</script>

<section class="card monitoring" aria-label="Service monitoring">
  <div class="heading">
    <div>
      <h2>Service Monitoring</h2>
      <p class="muted">Pushed live by Monitoring Service through SignalR.</p>
    </div>
  </div>
  {#if statuses.length === 0}
    <p class="muted">Waiting for Monitoring Service…</p>
  {:else}
    <div class="grid">
      {#each statuses as service (service.name)}
        <article class="service" data-status={service.status}>
          <div class="top">
            <strong>{service.name}</strong>
            <span class="pill" data-status={service.status}>{service.status}</span>
          </div>
          <span class="meta">{detail(service)}</span>
        </article>
      {/each}
    </div>
  {/if}
</section>

<style>
  .monitoring { margin-bottom: 1.5rem; }
  .heading h2 { margin: 0; }
  .heading p { margin: 0.25rem 0 1rem; }
  .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 0.9rem; }
  .service { border: 1px solid var(--border); border-left: 5px solid #94a3b8; border-radius: 12px; padding: 0.9rem 1rem; background: var(--surface); }
  .service[data-status='Healthy'] { border-left-color: #16a34a; }
  .service[data-status='Unhealthy'] { border-left-color: #dc2626; background: #fef2f2; }
  .top { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; }
  .pill { font-size: 0.75rem; font-weight: 800; padding: 0.2rem 0.55rem; border-radius: 999px; background: #e2e8f0; color: #475569; }
  .pill[data-status='Healthy'] { background: #dcfce7; color: #15803d; }
  .pill[data-status='Unhealthy'] { background: #fee2e2; color: #b91c1c; }
  .meta { display: block; margin-top: 0.4rem; color: var(--text-muted); font-size: 0.9rem; overflow-wrap: anywhere; }
</style>
