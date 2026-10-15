# 03 Communication

[Index](README.md) · [01 Overview](01-overview.md) · [02 Architecture](02-architecture.md) · [03 Communication](03-communication.md) · [04 Data model](04-data-model.md) · [05 API](05-api-reference.md) · [07 Design decisions](07-design-decisions.md)

## Register and log in

The same endpoint registers a new user on the first login and checks the password afterwards. The returned JWT
is sent with every later request.

```mermaid
sequenceDiagram
    autonumber
    actor U as User (browser)
    participant G as API Gateway
    participant US as User Service
    participant S as Any other service

    U->>G: POST /api/users/login {name, password, role}
    Note right of G: rate limit: 20 / minute / IP
    G->>US: forward
    alt name + role not registered yet
        US->>US: hash password (PBKDF2), new ID
        US-->>U: 201 {token, user}
    else registered and password correct
        US-->>U: 200 {token, user}
    else wrong password
        US-->>U: 401 Unauthorized
    end
    U->>G: later requests with "Authorization: Bearer <token>"
    G->>S: forward
    S->>S: verify signature, expiry and role
```

<sub>Source: [`diagrams/03-login-sequence.mmd`](diagrams/03-login-sequence.mmd) · Image: [`diagrams/03-login-sequence.png`](diagrams/03-login-sequence.png)</sub>

## Report an incident (real-time)

```mermaid
sequenceDiagram
    autonumber
    actor E as Employee (browser)
    participant G as API Gateway
    participant I as Incident Service
    participant N as Notification Service
    actor IT as IT Support (browser)

    Note over E,IT: Both browsers keep an open SignalR connection to the hub
    E->>G: POST /api/incidents (Bearer JWT)
    G->>I: forward request
    I->>I: validate token and input,<br/>reporter = user from token, save
    I->>N: POST /internal/notifications (X-Internal-Api-Key)
    I-->>G: 201 Created
    G-->>E: 201 Created
    N->>N: store in notification history
    par to IT Support
        N-)IT: IncidentChanged (group "it-support")
    and to the reporter only
        N-)E: IncidentChanged (group "user:{id}")
    end
    Note over E,IT: Other employees receive nothing. Pages update without reloading.
```

<sub>Source: [`diagrams/02-realtime-sequence.mmd`](diagrams/02-realtime-sequence.mmd) · Image: [`diagrams/02-realtime-sequence.png`](diagrams/02-realtime-sequence.png)</sub>

## Change status (real-time)

```mermaid
sequenceDiagram
    autonumber
    actor IT as IT Support (browser)
    participant G as API Gateway
    participant I as Incident Service
    participant N as Notification Service
    actor E as Reporter (browser)

    IT->>G: PATCH /api/incidents/{id}/status {"status":"In Progress"} (JWT)
    G->>I: forward
    I->>I: role must be IT Support (else 403),<br/>status must be an allowed value (else 400)
    I->>I: change status and record an update (under lock)
    I->>N: POST /internal/notifications (StatusChanged)
    I-->>IT: 200 OK (updated incident)
    N-)IT: IncidentChanged (group "it-support")
    N-)E: IncidentChanged (group "user:{reporterId}")
    E->>G: GET /api/incidents (reload)
    Note over E: Badge "New update" appears without reloading the page
```

<sub>Source: [`diagrams/09-status-change-sequence.mmd`](diagrams/09-status-change-sequence.mmd) · Image: [`diagrams/09-status-change-sequence.png`](diagrams/09-status-change-sequence.png)</sub>

Replies of the employee work the same way in the other direction: `POST /api/incidents/{id}/updates` →
event to the `it-support` group and to the reporter.

## SignalR groups

The hub (`/hubs/notifications`) requires a valid JWT. Because browsers cannot send headers on WebSocket
connections, the token is sent as the `access_token` query parameter, which the services accept only for `/hubs`
paths. When a connection opens, the hub puts it into a group:

| Connection | Group | Receives |
|---|---|---|
| IT Support user | `it-support` | Every incident event and the monitoring snapshot |
| Employee with ID 1001 | `user:1001` | Only events about incidents reported by user 1001 |

**The server decides who receives an event.** An employee's browser never receives anything about other
people's incidents.

| SignalR event | Sent to | Content |
|---|---|---|
| `IncidentChanged` | `it-support` + reporter's group | Type (`Created`, `StatusChanged`, `Updated`, `Deleted`), incident data, actor, message |
| `ServiceStatusUpdated` | `it-support` | Health of the three monitored services, every 5 seconds |

## Incident lifecycle

```mermaid
stateDiagram-v2
    direction LR
    InProgress: In Progress
    [*] --> Open: employee reports
    Open --> InProgress: IT Support
    InProgress --> Resolved: IT Support or monitoring
    Open --> Resolved: monitoring auto-resolve
    Resolved --> Closed: IT Support
    Closed --> [*]
```

<sub>Source: [`diagrams/04-incident-lifecycle.mmd`](diagrams/04-incident-lifecycle.mmd) · Image: [`diagrams/04-incident-lifecycle.png`](diagrams/04-incident-lifecycle.png)</sub>

Updates and replies can be added in any state. Only IT Support can change the status; Monitoring Service
resolves the incidents it opened automatically.

## Monitoring

Every 5 seconds Monitoring Service checks the three services **in parallel** (`GET /health`, 3-second timeout)
and pushes the snapshot to IT Support. A service becomes *Unhealthy* only after **two failed checks in a row**,
so a single slow answer or a service that is still starting does not cause a false alarm.

```mermaid
stateDiagram-v2
    direction TB
    [*] --> Unknown
    Unknown --> Healthy: check OK
    Healthy --> Unhealthy: 2 failed checks in a row
    Unknown --> Unhealthy: 2 failed checks in a row
    Unhealthy --> Healthy: check OK
    note right of Healthy
        A single failed check only increases a counter,
        the status stays Healthy.
    end note
    note right of Unhealthy
        Entering: a Critical incident is opened.
        Leaving: that incident is resolved automatically.
    end note
```

<sub>Source: [`diagrams/05-monitoring-states.mmd`](diagrams/05-monitoring-states.mmd) · Image: [`diagrams/05-monitoring-states.png`](diagrams/05-monitoring-states.png)</sub>

## Client connection

The client retries the first connection until it succeeds and reloads its data after every (re)connect, so
events that happened while it was offline are not lost.

```mermaid
stateDiagram-v2
    direction LR
    [*] --> connecting: page opened / signed in
    connecting --> live: connected
    connecting --> offline: failed
    offline --> connecting: retry after 3 s
    live --> reconnecting: connection lost
    reconnecting --> live: reconnected
    reconnecting --> connecting: gave up, start again
    live --> [*]: sign out
    note right of live
        After every (re)connect the client
        reloads its data, so no event is lost.
    end note
```

<sub>Source: [`diagrams/12-reconnect.mmd`](diagrams/12-reconnect.mmd) · Image: [`diagrams/12-reconnect.png`](diagrams/12-reconnect.png)</sub>

## Failure behaviour

### Notification Service is down

```mermaid
sequenceDiagram
    autonumber
    actor E as Employee
    participant I as Incident Service
    participant N as Notification Service (down)
    participant M as Monitoring Service

    E->>I: POST /api/incidents
    I->>I: save incident
    I-xN: POST /internal/notifications
    Note over I: fails or times out after 3 s,<br/>the error is only logged
    I-->>E: 201 Created (the incident is saved)
    M-xN: GET /health (2 checks in a row fail)
    M->>I: POST /internal/incidents (Critical: notification-service unavailable)
    Note over E,M: When the service is back, browsers reconnect automatically and reload their data,<br/>and Monitoring Service resolves its incident.
```

<sub>Source: [`diagrams/10-failure-notification-down.mmd`](diagrams/10-failure-notification-down.mmd) · Image: [`diagrams/10-failure-notification-down.png`](diagrams/10-failure-notification-down.png)</sub>

- Incidents are still created, updated and deleted; the publishing call times out after 3 seconds and is logged.
- Browsers show the connection as offline and keep retrying; when the service is back they reconnect and reload.

### User Service is down

```mermaid
sequenceDiagram
    autonumber
    participant M as Monitoring Service
    participant U as User Service
    participant I as Incident Service
    actor IT as IT Support (browser)

    M-xU: GET /health (failure 1, status stays Healthy)
    M-xU: GET /health (failure 2)
    M->>M: user-service → Unhealthy
    M->>I: POST /internal/incidents (Critical)
    I-)IT: new Critical incident (through Notification Service)
    Note over U: New logins fail. Users who are already signed in keep working:<br/>every service verifies the JWT itself.
    M->>U: GET /health (OK)
    M->>M: user-service → Healthy
    M->>I: POST /internal/incidents/{id}/resolve
    I-)IT: incident is Resolved
```

<sub>Source: [`diagrams/11-failure-user-service-down.mmd`](diagrams/11-failure-user-service-down.mmd) · Image: [`diagrams/11-failure-user-service-down.png`](diagrams/11-failure-user-service-down.png)</sub>

- New logins fail, but users who are already signed in keep working because every service verifies the JWT itself.

### Incident Service is down

- API calls about incidents return an error that the client shows to the user.
- Monitoring Service marks it *Unhealthy* but cannot open an automatic incident (the service that would store it
  is the one that is down); this is logged.

### API Gateway is down

- The client cannot reach anything and shows the connection as offline. It keeps retrying every 3 seconds and
  continues automatically when the gateway is back.

### Two requests change the same data at the same time

- Changes of an incident (status, updates) go through methods protected by a lock, so no update is lost.
  An integration test sends 50 updates in parallel and checks that all 50 are stored.
- "Find or create user" also runs under a lock, so two simultaneous first logins with the same name cannot
  create two users.
