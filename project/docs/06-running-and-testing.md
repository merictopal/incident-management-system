# 06 Running and testing

[Index](README.md) · [01 Overview](01-overview.md) · [02 Architecture](02-architecture.md) · [03 Communication](03-communication.md) · [04 Data model](04-data-model.md) · [05 API](05-api-reference.md) · [06 Running & testing](06-running-and-testing.md) · [07 Design decisions](07-design-decisions.md)

All commands and paths on this page are relative to the `project/` folder of the repository.

## Prerequisites

- .NET SDK 9.0
- Node.js 20 or newer
- Docker Desktop (only for the Docker option)

## Start

**Option 1 – Docker**

```bash
docker compose up --build
```

Open http://localhost:5173. Stop with `Ctrl+C` and `docker compose down`.

**Option 2 – VS Code**

Open the `project/` folder in VS Code and use **Terminal → Run Task…**:

| Task | What it does |
|---|---|
| 1. Start all services + client (local) | Builds and starts the five backend processes and the client |
| 2. Open the app in Microsoft Edge | Opens http://localhost:5173 |
| 3. Run all tests (build + unit + e2e) | Full test run (see below) |
| 4. Run tests with Docker | The same end-to-end tests against `docker compose` |
| 5. Stop all (local) | Stops everything that uses the project ports |

**Option 3 – PowerShell / double-click**

| Script | Shortcut | What it does |
|---|---|---|
| `scripts/start-local.ps1` | `START-LOCAL.cmd` | Start everything without Docker |
| `scripts/stop-local.ps1` | `STOP-LOCAL.cmd` | Stop everything |
| `scripts/test-all.ps1` | `RUN-TESTS.cmd` | Full test run |
| `scripts/test-docker.ps1` | `RUN-DOCKER-TESTS.cmd` | Test run with Docker |

**Option 4 – manually**: `dotnet run` in each service folder (ports come from `Properties/launchSettings.json`),
then `npm install` and `npm run dev` in `client/`.

## Automated tests

| Level | What is tested | Where |
|---|---|---|
| Integration tests (xUnit) | Each service is started in memory with `WebApplicationFactory` and called over HTTP | `tests/UserService.Tests`, `tests/IncidentService.Tests` |
| End-to-end tests | The running system through the gateway, with real SignalR connections for two employees and one IT Support user | `tests/e2e/e2e.mjs` |
| Monitoring scenario | User Service is stopped → becomes Unhealthy and a Critical incident is opened → restarted → Healthy and the incident is resolved | `tests/e2e/e2e.mjs` (`down` / `recover`) |

The end-to-end tests check, among others: registration and stable IDs, wrong password (401), missing token (401),
wrong role (403), hidden internal routes (404), CORS, that employee B does **not** receive employee A's events,
real-time status changes and replies, the notification history and the live monitoring push.

### Results

| Run (12 October 2026, .NET SDK 9.0.304, Windows) | Result |
|---|---|
| `dotnet build` (8 projects) | 0 warnings, 0 errors |
| xUnit integration tests | 22 / 22 passed |
| End-to-end tests, services started locally | 23 / 23 passed |
| Monitoring failure and recovery scenario | 4 / 4 passed |
| The same end-to-end tests against `docker compose` | 27 / 27 passed |
| Docker: only ports 5000 and 5173 published | confirmed |

Re-run on 11 November 2026 from the `project/` folder (`scripts/test-all.ps1`): build OK, 22 / 22 xUnit tests,
23 / 23 end-to-end checks and 4 / 4 monitoring checks passed again.

Each run writes a summary to `test-logs/summary.txt`.

## Demo script

1. Open http://localhost:5173 in two browser windows.
2. Window 1: sign in to the **IT Support workspace**. The three services appear as Healthy.
3. Window 2: sign in to the **Employee workspace** and report an incident. It appears in window 1 without reloading.
4. Window 1: change the status to *In Progress* and add an update. Window 2 shows both immediately.
5. Window 2: reply to IT Support. The reply appears in window 1.
6. Sign in as a second employee: the first employee's incident is not visible.
7. Stop one service (e.g. `docker compose stop user-service`): after about 10 seconds it becomes Unhealthy and a
   Critical incident appears; after `docker compose start user-service` the incident is resolved automatically.

## Troubleshooting

| Problem | Solution |
|---|---|
| "Ports already in use" | Run the *Stop all* task (or `STOP-LOCAL.cmd`), or `docker compose down` |
| Windows asks whether to run a `.cmd` file | Choose *Run anyway*, or use the VS Code tasks instead |
| Docker: "engine not running" | Start Docker Desktop and wait until it is ready |
| Data disappeared | Data is kept in memory; restarting a service clears it |
