<script lang="ts">
  import { onMount } from 'svelte';
  import * as api from '$lib/api';
  import { Workspace } from '$lib/workspace.svelte';
  import { STATUSES, type Incident, type IncidentStatus } from '$lib/types';
  import { formatDate, personLabel } from '$lib/format';
  import LoginCard from '$lib/components/LoginCard.svelte';
  import WorkspaceHeader from '$lib/components/WorkspaceHeader.svelte';
  import IncidentList from '$lib/components/IncidentList.svelte';
  import UpdateHistory from '$lib/components/UpdateHistory.svelte';
  import ServiceMonitor from '$lib/components/ServiceMonitor.svelte';
  import NotificationFeed from '$lib/components/NotificationFeed.svelte';
  import Badge from '$lib/components/Badge.svelte';

  const ws = new Workspace('IT Support');

  let statusFilter = $state<IncidentStatus | 'All'>('All');
  let updateText = $state('');
  let selectedId = $state<string | null>(null);
  const selected = $derived(ws.incidents.find((incident) => incident.id === selectedId) ?? null);
  const visible = $derived(
    statusFilter === 'All' ? ws.incidents : ws.incidents.filter((incident) => incident.status === statusFilter)
  );
  const openCount = $derived(ws.incidents.filter((incident) => incident.status === 'Open').length);

  onMount(() => {
    ws.restore();
    return () => ws.dispose();
  });

  function select(incident: Incident) {
    selectedId = incident.id;
    ws.markSeen(incident.id);
  }

  async function changeStatus(status: IncidentStatus) {
    if (!selected || status === selected.status) return;
    const id = selected.id;
    await ws.run((token) => api.changeStatus(token, id, status), `Status changed to ${status}.`);
  }

  async function addUpdate(event: SubmitEvent) {
    event.preventDefault();
    if (!selected || !updateText.trim()) return;
    const id = selected.id;
    if (await ws.run((token) => api.addUpdate(token, id, updateText), 'Update added.')) updateText = '';
  }

  async function remove() {
    if (!selected || !confirm(`Delete "${selected.title}"?`)) return;
    const id = selected.id;
    if (await ws.run((token) => api.deleteIncident(token, id), 'Incident deleted.')) selectedId = null;
  }
</script>

<svelte:head><title>IT Support Workspace</title></svelte:head>

<main>
  <a class="back" href="/">← Change workspace</a>

  {#if !ws.session}
    {#if ws.flash}<p class="flash {ws.flash.kind}">{ws.flash.text}</p>{/if}
    <LoginCard eyebrow="IT Support workspace" submitLabel="Continue as IT Support" onLogin={(n, p) => ws.login(n, p)} />
  {:else}
    <WorkspaceHeader eyebrow="IT Support workspace" title="Manage incidents" user={ws.session.user}
      connection={ws.connection} onLogout={() => ws.logout()} />

    <ServiceMonitor statuses={ws.services} />

    {#if ws.flash}<p class="flash {ws.flash.kind}" role="status">{ws.flash.text}</p>{/if}

    <div class="layout">
      <div class="stack">
        <section class="card">
          <div class="list-head">
            <h2>All incidents <span class="count">{openCount} open</span></h2>
            <select bind:value={statusFilter} aria-label="Filter by status">
              <option value="All">All statuses</option>
              {#each STATUSES as status}<option>{status}</option>{/each}
            </select>
          </div>
          <IncidentList incidents={visible} {selectedId} highlighted={ws.highlighted}
            emptyText="No incidents to show." onSelect={select} />
        </section>
        <NotificationFeed notifications={ws.notifications} />
      </div>

      <section class="card panel">
        {#if selected}
          <h2>{selected.title}</h2>
          <p class="badges"><Badge kind="status" value={selected.status} /><Badge kind="priority" value={selected.priority} /></p>
          <p class="desc">{selected.description || 'No description provided.'}</p>
          <p class="muted">Reported by {personLabel(selected.reporterId, selected.reporterName)} · {formatDate(selected.createdAt)}</p>

          <label>Status
            <select value={selected.status} onchange={(event) => void changeStatus(event.currentTarget.value as IncidentStatus)}>
              {#each STATUSES as status}<option>{status}</option>{/each}
            </select>
          </label>

          <form onsubmit={addUpdate}>
            <label>Add IT update<textarea bind:value={updateText} maxlength="1000" placeholder="Write an update for the reporter"></textarea></label>
            <div class="buttons">
              <button disabled={!updateText.trim()}>Add update</button>
              <button type="button" class="danger" onclick={() => void remove()}>Delete incident</button>
            </div>
          </form>

          <h3>History</h3>
          <UpdateHistory updates={selected.updates} />
        {:else}
          <p class="muted">Select an incident to manage it.</p>
        {/if}
      </section>
    </div>
  {/if}
</main>

<style>
  h2 { margin-top: 0; }
  .list-head { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; margin-bottom: 0.5rem; }
  .list-head h2 { margin: 0; }
  .count { font-size: 0.85rem; color: var(--text-muted); font-weight: 600; margin-left: 0.4rem; }
  .badges { display: flex; gap: 0.4rem; }
  .desc { white-space: pre-wrap; overflow-wrap: anywhere; }
  .buttons { display: flex; gap: 0.5rem; flex-wrap: wrap; }
</style>
