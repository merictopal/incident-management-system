<script lang="ts">
  type Props = { kind: 'status' | 'priority' | 'note'; value: string };
  let { kind, value }: Props = $props();
</script>

<i class="badge" data-kind={kind} data-value={value}>{value}</i>

<style>
  .badge { display: inline-flex; align-items: center; font-style: normal; font-size: 0.75rem; font-weight: 800; padding: 0.28rem 0.6rem; border-radius: 999px; letter-spacing: 0.02em; white-space: nowrap; background: var(--surface-muted); color: var(--text-muted); }
  [data-value='Open'] { background: #fff0e8; color: #c2410c; }
  [data-value='In Progress'] { background: #dbeafe; color: #1d4ed8; }
  [data-value='Resolved'] { background: #dcfce7; color: #15803d; }
  [data-value='Closed'] { background: #e2e8f0; color: #475569; }
  [data-kind='priority'][data-value='Low'] { background: #e0f2fe; color: #0369a1; }
  [data-kind='priority'][data-value='Medium'] { background: #fef3c7; color: #a16207; }
  [data-kind='priority'][data-value='High'] { background: #ffedd5; color: #c2410c; }
  [data-kind='priority'][data-value='Critical'] { background: #fee2e2; color: #b91c1c; }
  [data-kind='note'] { background: #ede9fe; color: #6d28d9; }
</style>
