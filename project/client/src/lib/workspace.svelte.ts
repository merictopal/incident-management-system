import * as api from './api';
import { ApiError } from './api';
import { connectRealtime, type ConnectionState } from './realtime';
import { clearSession, loadSession, saveSession, type Session } from './session';
import type { Incident, IncidentNotification, ServiceStatus, UserRole } from './types';

export type Flash = { kind: 'ok' | 'error'; text: string };

/**
 * State and behaviour shared by the Employee and IT Support workspaces:
 * login/logout, loading data, the SignalR connection and user messages.
 * The pages only contain their own markup and role-specific actions.
 */
export class Workspace {
  session = $state<Session | null>(null);
  incidents = $state<Incident[]>([]);
  notifications = $state<IncidentNotification[]>([]);
  services = $state<ServiceStatus[]>([]);
  connection = $state<ConnectionState>('offline');
  flash = $state<Flash | null>(null);
  /** Incidents that changed (by someone else) since the user last opened them. */
  highlighted = $state(new Set<string>());

  #realtime: { stop(): Promise<void> } | null = null;
  #flashTimer: ReturnType<typeof setTimeout> | undefined;

  constructor(public readonly role: UserRole) {}

  get isItSupport() {
    return this.role === 'IT Support';
  }

  /** Restores a stored session (call from onMount). */
  restore() {
    const stored = loadSession(this.role);
    if (stored) this.#start(stored);
  }

  async login(name: string, password: string) {
    const response = await api.login(name, password, this.role);
    const session = saveSession(response);
    this.show(
      'ok',
      response.isNewUser
        ? `Welcome, ${response.user.name}. Your user ID is #${response.user.id}.`
        : `Welcome back, ${response.user.name} (#${response.user.id}).`
    );
    this.#start(session);
  }

  logout(reason?: string) {
    void this.#realtime?.stop();
    this.#realtime = null;
    clearSession(this.role);
    this.session = null;
    this.incidents = [];
    this.notifications = [];
    this.services = [];
    this.highlighted = new Set();
    this.connection = 'offline';
    this.flash = reason ? { kind: 'error', text: reason } : null;
  }

  dispose() {
    void this.#realtime?.stop();
    this.#realtime = null;
  }

  /** Reloads everything from the services (also called after each reconnect). */
  async refresh() {
    const token = this.session?.token;
    if (!token) return;
    try {
      const [incidents, notifications, services] = await Promise.all([
        api.getIncidents(token),
        api.getNotifications(token),
        this.isItSupport ? api.getServiceStatuses(token) : Promise.resolve(this.services)
      ]);
      this.incidents = incidents;
      this.notifications = notifications;
      this.services = services;
    } catch (error) {
      this.#handleError(error);
    }
  }

  /** Runs an API action with the current token; shows success or error. Returns true on success. */
  async run(action: (token: string) => Promise<unknown>, successMessage?: string): Promise<boolean> {
    const token = this.session?.token;
    if (!token) return false;
    try {
      await action(token);
      if (successMessage) this.show('ok', successMessage);
      await this.refresh();
      return true;
    } catch (error) {
      this.#handleError(error);
      return false;
    }
  }

  markSeen(incidentId: string) {
    if (!this.highlighted.has(incidentId)) return;
    const next = new Set(this.highlighted);
    next.delete(incidentId);
    this.highlighted = next;
  }

  show(kind: Flash['kind'], text: string) {
    this.flash = { kind, text };
    clearTimeout(this.#flashTimer);
    if (kind === 'ok') this.#flashTimer = setTimeout(() => (this.flash = null), 6000);
  }

  #start(session: Session) {
    this.session = session;
    void this.refresh();

    void this.#realtime?.stop();
    this.#realtime = connectRealtime(session.token, {
      onState: (state) => (this.connection = state),
      onConnected: () => void this.refresh(),
      onUnauthorized: () => this.logout('Your session has expired. Please sign in again.'),
      onServiceStatus: (statuses) => (this.services = statuses),
      onIncidentChanged: (event) => this.#onIncidentChanged(event)
    });
  }

  #onIncidentChanged(event: IncidentNotification) {
    this.notifications = [event, ...this.notifications].slice(0, 50);

    const byMe = event.actorId === String(this.session?.user.id);
    if (!byMe) {
      this.show('ok', `Real-time update: ${event.message}`);
      if (event.type !== 'Deleted') this.highlighted = new Set([...this.highlighted, event.incidentId]);
    }
    void this.refresh();
  }

  #handleError(error: unknown) {
    if (error instanceof ApiError && error.status === 401) {
      this.logout('Your session has expired. Please sign in again.');
      return;
    }
    this.show('error', error instanceof Error ? error.message : 'Something went wrong.');
  }
}
