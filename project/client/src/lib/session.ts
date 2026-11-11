import type { LoginResponse, UserIdentity, UserRole } from './types';

/** What the browser keeps after a successful login (one session per role). */
export type Session = { token: string; expiresAt: string; user: UserIdentity };

const storageKey = (role: UserRole) => `incident-session-${role}`;

export function loadSession(role: UserRole): Session | null {
  try {
    const raw = localStorage.getItem(storageKey(role));
    if (!raw) return null;
    const session = JSON.parse(raw) as Session;
    const valid = session.user?.role === role && new Date(session.expiresAt).getTime() > Date.now();
    if (!valid) localStorage.removeItem(storageKey(role));
    return valid ? session : null;
  } catch {
    return null;
  }
}

export function saveSession(response: LoginResponse): Session {
  const session: Session = { token: response.token, expiresAt: response.expiresAt, user: response.user };
  try {
    localStorage.setItem(storageKey(response.user.role), JSON.stringify(session));
  } catch {
    // Private mode / blocked storage: the session simply lasts until the tab is closed.
  }
  return session;
}

export function clearSession(role: UserRole) {
  try {
    localStorage.removeItem(storageKey(role));
  } catch {
    /* ignore */
  }
}
