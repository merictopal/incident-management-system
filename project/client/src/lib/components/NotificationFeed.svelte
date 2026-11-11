<script lang="ts">
  import { timeAgo } from '$lib/format';
  import type { IncidentNotification } from '$lib/types';

  let { notifications }: { notifications: IncidentNotification[] } = $props();
</script>

<section class="card feed" aria-label="Recent activity">
  <h2>Recent activity</h2>
  {#if notifications.length === 0}
    <p class="muted">No activity yet.</p>
  {:else}
    <ul>
      {#each notifications.slice(0, 8) as item, index (item.incidentId + item.occurredAt + index)}
        <li data-type={item.type}><span>{item.message}</span><time>{timeAgo(item.occurredAt)}</time></li>
      {/each}
    </ul>
  {/if}
</section>

<style>
  h2 { margin-top: 0; }
  ul { list-style: none; margin: 0; padding: 0; }
  li { display: flex; justify-content: space-between; gap: 1rem; padding: 0.55rem 0; border-top: 1px solid var(--border); font-size: 0.92rem; }
  li span { overflow-wrap: anywhere; }
  li[data-type='Deleted'] span { color: var(--text-muted); text-decoration: line-through; }
  time { color: var(--text-muted); white-space: nowrap; font-size: 0.82rem; }
</style>
