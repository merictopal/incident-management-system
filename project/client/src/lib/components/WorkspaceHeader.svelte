<script lang="ts">
  import type { ConnectionState } from '$lib/realtime';
  import type { UserIdentity } from '$lib/types';

  type Props = { eyebrow: string; title: string; user: UserIdentity; connection: ConnectionState; onLogout: () => void };
  let { eyebrow, title, user, connection, onLogout }: Props = $props();

  const labels: Record<ConnectionState, string> = {
    connecting: 'Connecting…',
    live: 'Live',
    reconnecting: 'Reconnecting…',
    offline: 'Offline – retrying'
  };
</script>

<header>
  <div>
    <p class="eyebrow">{eyebrow}</p>
    <h1>{title}</h1>
    <p class="identity"><b>#{user.id}</b> {user.name} · {user.role}</p>
  </div>
  <div class="actions">
    <span class="connection" data-state={connection} title="Real-time connection (SignalR)">● {labels[connection]}</span>
    <button class="secondary" onclick={onLogout}>Log out</button>
  </div>
</header>

<style>
  header { display: flex; justify-content: space-between; align-items: flex-end; gap: 1rem; flex-wrap: wrap; margin-bottom: 1.25rem; }
  h1 { margin: 0.2rem 0; }
  .identity { color: var(--text-muted); margin: 0; }
  .actions { display: flex; align-items: center; gap: 0.75rem; }
  .connection { font-weight: 800; font-size: 0.85rem; color: #b45309; }
  .connection[data-state='live'] { color: #15803d; }
  .connection[data-state='offline'] { color: #b91c1c; }
</style>
