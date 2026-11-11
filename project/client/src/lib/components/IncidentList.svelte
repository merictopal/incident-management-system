<script lang="ts">
  import Badge from './Badge.svelte';
  import { personLabel, timeAgo } from '$lib/format';
  import type { Incident } from '$lib/types';

  type Props = {
    incidents: Incident[];
    selectedId: string | null;
    highlighted?: Set<string>;
    emptyText: string;
    onSelect: (incident: Incident) => void;
  };
  let { incidents, selectedId, highlighted = new Set(), emptyText, onSelect }: Props = $props();
</script>

{#if incidents.length === 0}
  <p class="muted">{emptyText}</p>
{:else}
  <ul>
    {#each incidents as incident (incident.id)}
      <li>
        <button class="item" class:selected={selectedId === incident.id} onclick={() => onSelect(incident)}>
          <strong>{incident.title}</strong>
          <span class="sub">{personLabel(incident.reporterId, incident.reporterName)} · {timeAgo(incident.createdAt)}</span>
          <span class="badges">
            <Badge kind="status" value={incident.status} />
            <Badge kind="priority" value={incident.priority} />
            {#if highlighted.has(incident.id)}<Badge kind="note" value="New update" />{/if}
          </span>
        </button>
      </li>
    {/each}
  </ul>
{/if}

<style>
  ul { list-style: none; margin: 0; padding: 0; }
  .item { display: block; width: 100%; text-align: left; background: transparent; color: var(--text); border: 0; border-bottom: 1px solid var(--border); border-radius: 0; padding: 0.9rem 0.75rem; box-shadow: none; font-weight: 400; }
  .item:hover, .item.selected { background: var(--accent-soft); }
  strong { display: block; }
  .sub { display: block; color: var(--text-muted); font-size: 0.9rem; margin-top: 0.25rem; }
  .badges { display: flex; flex-wrap: wrap; gap: 0.4rem; margin-top: 0.5rem; }
</style>
