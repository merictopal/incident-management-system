<script lang="ts">
  import { onMount } from 'svelte';
  import * as api from '$lib/api';
  import { Workspace } from '$lib/workspace.svelte';
  import { PRIORITIES, type Incident, type IncidentPriority } from '$lib/types';
  import { formatDate } from '$lib/format';
  import LoginCard from '$lib/components/LoginCard.svelte';
  import WorkspaceHeader from '$lib/components/WorkspaceHeader.svelte';
  import IncidentList from '$lib/components/IncidentList.svelte';
  import UpdateHistory from '$lib/components/UpdateHistory.svelte';
  import NotificationFeed from '$lib/components/NotificationFeed.svelte';
  import Badge from '$lib/components/Badge.svelte';

  const ws = new Workspace('Employee');

  let title = $state('');
  let description = $state('');
  let priority = $state<IncidentPriority>('Medium');
  let reply = $state('');
  let selectedId = $state<string | null>(null);
  const selected = $derived(ws.incidents.find((incident) => incident.id === selectedId) ?? null);

  onMount(() => {
    ws.restore();
    return () => ws.dispose();
  });

  function select(incident: Incident) {
    selectedId = incident.id;
    ws.markSeen(incident.id);
  }

  async function report(event: SubmitEvent) {
    event.preventDefault();
    const ok = await ws.run(
      (token) => api.createIncident(token, { title, description, priority }),
      'Incident reported. IT Support has been notified.'
    );
    if (ok) {
      title = '';
      description = '';
      priority = 'Medium';
    }
  }

  async function sendReply(event: SubmitEvent) {
    event.preventDefault();
    if (!selected || !reply.trim()) return;
    const id = selected.id;
    if (await ws.run((token) => api.addUpdate(token, id, reply), 'Your reply was sent.')) reply = '';
  }
</script>

<svelte:head><title>Employee Workspace</title></svelte:head>

<main>
  <a class="back" href="/">← Change workspace</a>

  {#if !ws.session}
    {#if ws.flash}<p class="flash {ws.flash.kind}">{ws.flash.text}</p>{/if}
    <LoginCard eyebrow="Employee workspace" submitLabel="Continue as Employee" onLogin={(n, p) => ws.login(n, p)} />
  {:else}
    <WorkspaceHeader eyebrow="Employee workspace" title="Report an incident" user={ws.session.user}
      connection={ws.connection} onLogout={() => ws.logout()} />

    {#if ws.flash}<p class="flash {ws.flash.kind}" role="status">{ws.flash.text}</p>{/if}

    <div class="layout">
      <div class="stack">
        <form class="card" onsubmit={report}>
          <h2>New incident</h2>
          <label>Title<input bind:value={title} required maxlength="120" placeholder="Example: VPN connection unavailable" /></label>
          <label>Description<textarea bind:value={description} maxlength="2000"></textarea></label>
          <label>Priority
            <select bind:value={priority}>
              {#each PRIORITIES as option}<option>{option}</option>{/each}
            </select>
          </label>
          <button>Report incident</button>
        </form>
        <NotificationFeed notifications={ws.notifications} />
      </div>

      <div class="stack">
        <section class="card">
          <h2>My incidents</h2>
          <IncidentList incidents={ws.incidents} {selectedId} highlighted={ws.highlighted}
            emptyText="You have not reported any incidents yet." onSelect={select} />
        </section>

        {#if selected}
          <section class="card detail">
            <button class="secondary close" onclick={() => (selectedId = null)}>Close</button>
            <h2>{selected.title}</h2>
            <p class="badges"><Badge kind="status" value={selected.status} /><Badge kind="priority" value={selected.priority} /></p>
            <p class="desc">{selected.description || 'No description provided.'}</p>
            <p class="muted">Reported {formatDate(selected.createdAt)}</p>
            <h3>Updates</h3>
            <UpdateHistory updates={selected.updates} />
            <form onsubmit={sendReply}>
              <label>Reply to IT Support<textarea bind:value={reply} maxlength="1000" placeholder="Add more details or answer a question"></textarea></label>
              <button disabled={!reply.trim()}>Send reply</button>
            </form>
          </section>
        {/if}
      </div>
    </div>
  {/if}
</main>

<style>
  .detail { position: relative; }
  .close { float: right; }
  .badges { display: flex; gap: 0.4rem; }
  .desc { white-space: pre-wrap; overflow-wrap: anywhere; }
  h2 { margin-top: 0; }
</style>
