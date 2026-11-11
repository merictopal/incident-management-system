export type UserRole = 'Employee' | 'IT Support';

export type UserIdentity = { id: number; name: string; role: UserRole; createdAt: string };

export type LoginResponse = { token: string; expiresAt: string; user: UserIdentity; isNewUser: boolean };

export type IncidentStatus = 'Open' | 'In Progress' | 'Resolved' | 'Closed';
export type IncidentPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export const STATUSES: IncidentStatus[] = ['Open', 'In Progress', 'Resolved', 'Closed'];
export const PRIORITIES: IncidentPriority[] = ['Low', 'Medium', 'High', 'Critical'];

export type IncidentUpdate = {
  id: string;
  message: string;
  authorId: string;
  authorName: string;
  createdAt: string;
};

export type Incident = {
  id: string;
  title: string;
  description: string;
  priority: IncidentPriority;
  status: IncidentStatus;
  reporterId: string;
  reporterName: string;
  createdAt: string;
  updatedAt: string;
  updates: IncidentUpdate[];
};

export type IncidentEventType = 'Created' | 'StatusChanged' | 'Updated' | 'Deleted';

/** Real-time event pushed by Notification Service. */
export type IncidentNotification = {
  type: IncidentEventType;
  incidentId: string;
  title: string;
  priority: IncidentPriority;
  status: IncidentStatus;
  reporterId: string;
  reporterName: string;
  actorId: string;
  actorName: string;
  message: string;
  occurredAt: string;
};

export type ServiceHealth = 'Healthy' | 'Unhealthy' | 'Unknown';

export type ServiceStatus = {
  name: string;
  status: ServiceHealth;
  lastCheckedAt: string;
  responseTimeMs: number;
  consecutiveFailures: number;
  error: string | null;
};
