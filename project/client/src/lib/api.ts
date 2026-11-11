import { apiUrl } from './config';
import type {
  Incident,
  IncidentNotification,
  IncidentPriority,
  IncidentStatus,
  LoginResponse,
  ServiceStatus,
  UserRole
} from './types';

/** Error with the HTTP status and a message that can be shown to the user. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string
  ) {
    super(message);
  }
}

type RequestOptions = { method?: string; body?: unknown; token?: string };

async function request<T>(path: string, { method = 'GET', body, token }: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers['Authorization'] = `Bearer ${token}`;

  let response: Response;
  try {
    response = await fetch(`${apiUrl}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body)
    });
  } catch {
    throw new ApiError(0, 'Cannot reach the server. Is the API Gateway running?');
  }

  if (!response.ok) throw new ApiError(response.status, await readError(response));
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

/** Turns ASP.NET Core problem-details responses into a readable sentence. */
async function readError(response: Response): Promise<string> {
  if (response.status === 429) return 'Too many attempts. Please wait a minute and try again.';
  if (response.status === 403) return 'You are not allowed to do this.';

  try {
    const problem = await response.json();
    if (problem.errors) return Object.values(problem.errors as Record<string, string[]>).flat().join(' ');
    if (problem.detail) return problem.detail;
    if (problem.title) return problem.title;
  } catch {
    /* body was not JSON */
  }
  if (response.status === 401) return 'Your session has expired. Please log in again.';
  if (response.status === 404) return 'The item was not found. It may have been deleted.';
  return `Request failed (HTTP ${response.status}).`;
}

// ---- User Service ----
export const login = (name: string, password: string, role: UserRole) =>
  request<LoginResponse>('/api/users/login', { method: 'POST', body: { name, password, role } });

// ---- Incident Service ----
export const getIncidents = (token: string) => request<Incident[]>('/api/incidents', { token });

export const createIncident = (
  token: string,
  input: { title: string; description: string; priority: IncidentPriority }
) => request<Incident>('/api/incidents', { method: 'POST', body: input, token });

export const addUpdate = (token: string, incidentId: string, message: string) =>
  request<Incident>(`/api/incidents/${incidentId}/updates`, { method: 'POST', body: { message }, token });

export const changeStatus = (token: string, incidentId: string, status: IncidentStatus) =>
  request<Incident>(`/api/incidents/${incidentId}/status`, { method: 'PATCH', body: { status }, token });

export const deleteIncident = (token: string, incidentId: string) =>
  request<void>(`/api/incidents/${incidentId}`, { method: 'DELETE', token });

// ---- Notification Service ----
export const getNotifications = (token: string) =>
  request<IncidentNotification[]>('/api/notifications', { token });

// ---- Monitoring Service ----
export const getServiceStatuses = (token: string) =>
  request<ServiceStatus[]>('/api/monitoring/services', { token });
