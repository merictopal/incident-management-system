const timeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });

export function formatDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : timeFormat.format(date);
}

/** "just now", "5 min ago", "2 h ago", or a date for older items. */
export function timeAgo(value: string, now = Date.now()): string {
  const seconds = Math.round((now - new Date(value).getTime()) / 1000);
  if (seconds < 45) return 'just now';
  if (seconds < 3600) return `${Math.round(seconds / 60)} min ago`;
  if (seconds < 86400) return `${Math.round(seconds / 3600)} h ago`;
  return formatDate(value);
}

/** Monitoring-service reporter IDs are not numbers, so only prefix numeric IDs with "#". */
export function personLabel(id: string, name: string): string {
  return /^\d+$/.test(id) ? `#${id} ${name}` : name;
}
