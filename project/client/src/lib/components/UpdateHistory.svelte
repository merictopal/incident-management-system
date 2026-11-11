<script lang="ts">
  import { formatDate, personLabel } from '$lib/format';
  import type { IncidentUpdate } from '$lib/types';

  let { updates }: { updates: IncidentUpdate[] } = $props();
</script>

{#if updates.length === 0}
  <p class="muted">No updates yet.</p>
{:else}
  <ol>
    {#each updates as update (update.id)}
      <li>
        <div class="head"><b>{personLabel(update.authorId, update.authorName)}</b><time>{formatDate(update.createdAt)}</time></div>
        <p>{update.message}</p>
      </li>
    {/each}
  </ol>
{/if}

<style>
  ol { list-style: none; margin: 0; padding: 0; }
  li { border-top: 1px solid var(--border); padding: 0.75rem 0; }
  .head { display: flex; justify-content: space-between; gap: 1rem; flex-wrap: wrap; }
  time { color: var(--text-muted); font-size: 0.85rem; }
  p { margin: 0.3rem 0 0; white-space: pre-wrap; overflow-wrap: anywhere; }
</style>
