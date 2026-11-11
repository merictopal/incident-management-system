import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { apiUrl } from './config';
import type { IncidentNotification, ServiceStatus } from './types';

export type ConnectionState = 'connecting' | 'live' | 'reconnecting' | 'offline';

export type RealtimeHandlers = {
  onIncidentChanged?: (event: IncidentNotification) => void;
  onServiceStatus?: (statuses: ServiceStatus[]) => void;
  onState?: (state: ConnectionState) => void;
  /** Called after every successful (re)connect — reload data to catch events missed while offline. */
  onConnected?: () => void;
  /** Called when the server rejects the token (expired session). */
  onUnauthorized?: () => void;
};

const RETRY_DELAY_MS = 3000;
const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Opens the SignalR connection through the API Gateway.
 * Unlike a plain `start()`, the first connection attempt is retried until it
 * succeeds, so real-time updates work even if the backend starts after the page.
 */
export function connectRealtime(token: string, handlers: RealtimeHandlers) {
  const connection = new HubConnectionBuilder()
    .withUrl(`${apiUrl}/hubs/notifications`, { accessTokenFactory: () => token })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 10000])
    .configureLogging(LogLevel.Warning)
    .build();

  let stopped = false;
  const setState = (state: ConnectionState) => handlers.onState?.(state);

  connection.on('IncidentChanged', (event: IncidentNotification) => handlers.onIncidentChanged?.(event));
  connection.on('ServiceStatusUpdated', (statuses: ServiceStatus[]) => handlers.onServiceStatus?.(statuses));

  connection.onreconnecting(() => setState('reconnecting'));
  connection.onreconnected(() => {
    setState('live');
    handlers.onConnected?.();
  });
  // Automatic reconnect gave up (or the server closed the connection): keep trying.
  connection.onclose(() => {
    if (!stopped) void startWithRetry();
  });

  async function startWithRetry() {
    while (!stopped) {
      setState('connecting');
      try {
        await connection.start();
        setState('live');
        handlers.onConnected?.();
        return;
      } catch (error) {
        if (String(error).includes('401')) {
          stopped = true;
          setState('offline');
          handlers.onUnauthorized?.();
          return;
        }
        setState('offline');
        await delay(RETRY_DELAY_MS);
      }
    }
  }

  void startWithRetry();

  return {
    async stop() {
      stopped = true;
      await connection.stop();
    }
  };
}
