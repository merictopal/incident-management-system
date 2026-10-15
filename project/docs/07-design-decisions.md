# 07 Design decisions

[Index](README.md) · [01 Overview](01-overview.md) · [02 Architecture](02-architecture.md) · [03 Communication](03-communication.md) · [04 Data model](04-data-model.md) · [05 API](05-api-reference.md) · [07 Design decisions](07-design-decisions.md)

## Decisions at a glance

| Decision | Alternative | Why this one |
|---|---|---|
| ASP.NET Core SignalR for real-time | Plain WebSocket, polling | Reconnect, fallback transports and groups are built in |
| Server-side SignalR groups | Broadcast to everyone and filter in the browser | A browser should never receive data its user may not see |
| API Gateway (YARP) | Browser calls every service directly | One address for the client; CORS and rate limiting in one place; internal routes stay hidden |
| JWT verified by every service | Ask User Service on every request | No extra network call; still secure if the gateway is bypassed |
| Password + PBKDF2 | Name-only login | Prevents signing in as someone else |
| HTTP calls with a 3 s timeout between services | Message broker | Simple for a prototype; failures are caught and logged |
| Two failed checks before *Unhealthy* | Alarm on the first failure | Avoids false Critical incidents from slow answers and start-up |
| Automatic resolution of monitoring incidents | Manual only | The incident list reflects the real state of the system |
| In-memory storage, one store per service | Shared database | Keeps the prototype simple while respecting "each service owns its data" |
| Shared `ServiceDefaults` library | Copy the same code into every service | Same security setup everywhere; contains no business logic |
| Only gateway and client published in Docker | Publish every service | Internal services cannot be reached from outside |

## Known limitations

- Data is kept in memory; restarting a service clears it. Next step: one database per service.
- Calls between services are synchronous HTTP. If the Notification Service is down, events created in that time
  are not delivered later (the data itself is safe). A message broker (e.g. RabbitMQ) would fix this.
- There is no password reset and no user administration screen.
- Tokens cannot be revoked before they expire (8 hours).
- After a restart of the User Service, IDs start again at 1001.

## Likely questions

**Why is this a microservice system and not a monolith?**
Four separately built and deployed applications, each with its own process, container and data, talking only
over the network. One can fail while the others keep running.

**How does real-time communication work?**
Each browser keeps a SignalR (WebSocket) connection to the Notification Service. Incident Service reports every
change to Notification Service, which pushes it to the right groups. The browser does not have to ask.

**Can an employee see someone else's incident?**
No. Incident Service always filters by the employee's own ID, returns 404 for other incidents, and SignalR events
only go to IT Support and the reporter. All three are covered by automated tests.

**What happens if a service fails?**
See [failure behaviour](03-communication.md#failure-behaviour). In short: the other services keep working,
Monitoring Service opens a Critical incident and resolves it when the service is back.

**How are passwords stored?**
As PBKDF2-SHA256 hashes with a random salt per user and 100 000 iterations; they are compared in constant time.

