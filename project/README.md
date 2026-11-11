# Real-Time Incident Management System

A microservice-based web application for reporting, tracking and resolving IT incidents in real time.

**Problem.** In many organisations IT problems are reported by e-mail or chat. Reports get lost, employees do not
know whether anyone is working on their issue, and IT Support notices a failing backend service only when users
start complaining.

**Users.**
* **Employees** report incidents and follow the status of their own incidents live, without reloading the page.
* **IT Support** sees every incident, changes status and priority, replies to employees and watches the health of
  the backend services on a live dashboard.

**Solution in short.** Four .NET microservices (User, Incident, Notification, Monitoring) behind a YARP API gateway,
a SvelteKit web client, and real-time updates over SignalR. The Monitoring Service checks every service and opens a
Critical incident automatically when one goes down.

**Status.** Complete: all four services, the API gateway, the web client, Docker setup, automated tests and
documentation are in this folder.

## Quick start

From this `project/` folder:

```powershell
docker compose up --build        # option 1: Docker, then open http://localhost:5173
.\scripts\start-local.ps1         # option 2: without Docker (.NET 9 SDK + Node.js 20)
.\scripts\test-all.ps1            # build + unit tests + end-to-end tests
```

More options (VS Code tasks, double-click launchers) and the test results are in
[06 Running and testing](docs/06-running-and-testing.md).

## Folder structure

| Folder | Contents |
| --- | --- |
| `services/` | User, Incident, Notification and Monitoring services (.NET 9 minimal APIs) |
| `api-gateway/` | YARP reverse proxy: single entry point, CORS, login rate limit |
| `shared/ServiceDefaults/` | Cross-cutting code: JWT setup, internal API key, message contracts |
| `client/` | SvelteKit web client with Employee and IT Support workspaces |
| `tests/` | xUnit integration tests and end-to-end tests (REST + SignalR) |
| `scripts/` | PowerShell scripts to start, stop and test the system |
| `docs/` | Documentation with Mermaid diagrams |

## Timeline

| Week | Goal / Milestone | Verifiable Result |
| --- | --- | --- |
| 12–18 Oct | Architecture and documentation | [`docs/`](docs/README.md) with Mermaid diagrams |
| 19–25 Oct | Backend microservices and API gateway | Services and gateway in `project/` |
| 26 Oct – 1 Nov | Web client and real-time communication | Client in `project/`, live updates working |
| 2–8 Nov | Docker setup, tests, final documentation | `docker compose up` runs the system, tests pass |

## GitHub Projects

Project board: [Incident Management System](https://github.com/users/merictopal/projects/2) – tasks for each timeline milestone.

## Additional documents

| Document | Contents |
| --- | --- |
| [Documentation index](docs/README.md) | Overview of all pages and the high-level architecture |
| [01 Overview](docs/01-overview.md) | Requirements, system context, services, use cases, technology |
| [02 Architecture](docs/02-architecture.md) | Container and component views (C4), internal communication, deployment, security model |
| [03 Communication](docs/03-communication.md) | Real-time flows, login, SignalR groups, monitoring, failure behaviour |
| [04 Data model](docs/04-data-model.md) | ER diagram, data owned by each service, message contracts, validation |
| [05 API reference](docs/05-api-reference.md) | Gateway routing, endpoints with access rules, SignalR events |
| [06 Running and testing](docs/06-running-and-testing.md) | Start/stop, automated tests and results, demo script |
| [07 Design decisions](docs/07-design-decisions.md) | Decisions, known limitations, likely questions |

All diagrams are written in Mermaid; their sources are in [`docs/diagrams/`](docs/diagrams/).
