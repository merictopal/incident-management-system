# 05 API reference

[Index](README.md) · [01 Overview](01-overview.md) · [02 Architecture](02-architecture.md) · [03 Communication](03-communication.md) · [04 Data model](04-data-model.md) · [05 API](05-api-reference.md) · [07 Design decisions](07-design-decisions.md)

All public endpoints are reached through the gateway at `http://localhost:5000`. Errors use the standard
ASP.NET Core *problem details* format (`400` invalid input, `401` missing or invalid token, `403` wrong role,
`404` not found or not visible, `429` too many login attempts).

## Gateway routing

| Path | Service |
|---|---|
| `POST /api/users/login` | User Service (rate limited) |
| `/api/users/...` | User Service |
| `/api/incidents/...` | Incident Service |
| `/api/notifications/...` | Notification Service |
| `/hubs/notifications/...` (WebSocket) | Notification Service |
| `/api/monitoring/...` | Monitoring Service |
| `/internal/...` | **not routed** (404) |

## Authentication

Send the token from the login response in every request:

```
Authorization: Bearer <token>
```

SignalR connections send it as `?access_token=<token>`.

## User Service

| Method | Path | Access | Description |
|---|---|---|---|
| POST | `/api/users/login` | anyone | Body `{ "name", "password", "role" }`. 201 on first login (registration), 200 afterwards, 401 on wrong password. Returns `{ token, expiresAt, user, isNewUser }` |
| GET | `/api/users/me` | logged in | Current user |
| GET | `/api/users` | IT Support | All users |
| GET | `/api/users/{id}` | IT Support or the user | One user |

## Incident Service

| Method | Path | Access | Description |
|---|---|---|---|
| GET | `/api/incidents?status=` | logged in | IT Support: all incidents (optional `reporterId`, `status`). Employee: own incidents only |
| GET | `/api/incidents/{id}` | IT Support or reporter | One incident with its updates (others get 404) |
| POST | `/api/incidents` | logged in | Body `{ "title", "description", "priority" }`. Reporter is taken from the token |
| POST | `/api/incidents/{id}/updates` | IT Support or reporter | Body `{ "message" }` |
| PATCH | `/api/incidents/{id}/status` | IT Support | Body `{ "status" }` |
| DELETE | `/api/incidents/{id}` | IT Support | Delete |
| POST | `/internal/incidents` | internal key | Automatic incident (Monitoring Service) |
| POST | `/internal/incidents/{id}/resolve` | internal key | Automatic resolution (Monitoring Service) |

## Notification Service

| Method | Path | Access | Description |
|---|---|---|---|
| GET | `/api/notifications?take=` | logged in | Latest notifications (default 30, max. 100); employees only see their own |
| WS | `/hubs/notifications` | logged in | SignalR hub, events `IncidentChanged` and `ServiceStatusUpdated` |
| POST | `/internal/notifications` | internal key | Incident event from Incident Service |
| POST | `/internal/service-status` | internal key | Snapshot from Monitoring Service |

## Monitoring Service

| Method | Path | Access | Description |
|---|---|---|---|
| GET | `/api/monitoring/services` | IT Support | Current health of User, Incident and Notification Service |

Every service (and the gateway) also exposes an anonymous `GET /health`.

## Trying the API with curl

```bash
# 1. Log in (registers the user the first time) and copy the token from the response
curl -X POST http://localhost:5000/api/users/login \
     -H "Content-Type: application/json" \
     -d '{"name":"Meric","password":"secret1","role":"Employee"}'

# 2. Report an incident
curl -X POST http://localhost:5000/api/incidents \
     -H "Authorization: Bearer <token>" -H "Content-Type: application/json" \
     -d '{"title":"VPN connection unavailable","description":"Since this morning","priority":"High"}'

# 3. List my incidents
curl http://localhost:5000/api/incidents -H "Authorization: Bearer <token>"
```
