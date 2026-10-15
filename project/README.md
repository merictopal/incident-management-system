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

**Status.** The design and documentation are complete (see below). The implementation is added to this folder
step by step.

## Timeline

| Week | Goal / Milestone | Verifiable Result |
| --- | --- | --- |
| 12–18 Oct | Architecture and documentation | [`docs/`](docs/README.md) with Mermaid diagrams |
| 19–25 Oct | Backend microservices and API gateway | Services and gateway in `project/` |
| 26 Oct – 1 Nov | Web client and real-time communication | Client in `project/`, live updates working |
| 2–8 Nov | Docker setup, tests, final documentation | `docker compose up` runs the system, tests pass |

## GitHub Projects

Project board: *link will be added here.*

## Additional documents

| Document | Contents |
| --- | --- |
| [Documentation index](docs/README.md) | Overview of all pages and the high-level architecture |
| [01 Overview](docs/01-overview.md) | Requirements, system context, services, use cases, technology |
| [02 Architecture](docs/02-architecture.md) | Container and component views (C4), internal communication, deployment, security model |
| [03 Communication](docs/03-communication.md) | Real-time flows, login, SignalR groups, monitoring, failure behaviour |
| [04 Data model](docs/04-data-model.md) | ER diagram, data owned by each service, message contracts, validation |
| [05 API reference](docs/05-api-reference.md) | Gateway routing, endpoints with access rules, SignalR events |
| [07 Design decisions](docs/07-design-decisions.md) | Decisions, known limitations, likely questions |

All diagrams are written in Mermaid; their sources are in [`docs/diagrams/`](docs/diagrams/).
