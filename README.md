# Argus

Multi-host monitoring and log-shipping platform for Windows, Linux, and macOS hosts.

Agents collect resource usage (CPU/RAM/disk), per-process info, OS event logs, and tail
configurable log files, streaming everything to a central API. A web UI shows live system
pressure and a searchable log viewer.

## Components

| Project | Name | Stack | Role |
|---|---|---|---|
| `src/Argus.Herald` | **herald** | .NET 10 Worker Service | Cross-platform agent (Windows / Linux / macOS): collects metrics + logs, streams to styx over gRPC |
| `src/Argus.Styx` | **styx** | .NET 10 ASP.NET Core | gRPC ingest (from herald) + REST/SignalR query API (to pantheon) |
| `src/Argus.Codex` | **codex** | .NET 10 + EF Core | Entities, `ArgusDbContext` (SQL Server), migrations, retention |
| `src/pantheon` | **pantheon** | Angular 19 + PrimeNG | Fleet dashboard, host/process detail, log viewer |
| `src/Argus.Contracts` | — | .NET 10 | Shared gRPC `.proto` contracts |

**Data flow:** herald → (gRPC :8081, API key) → styx → (EF Core) → SQL Server.
pantheon ← (REST :8080 + SignalR `/hubs/live`) ← styx.

## Key design points

- **Transport:** herald streams to styx via gRPC client-streaming. Each agent authenticates
  with a per-agent API key in the `x-argus-api-key` header (allowlist in styx `Ingest:ApiKeys`;
  remove a key to revoke an agent).
- **Live UI:** styx pushes new metrics/processes/log lines to pantheon over SignalR.
- **Retention:** configurable `Retention:Days` (clamped 1–30, default 7); a daily background
  purge in styx deletes older rows.
- **Auth (UI):** none today. The REST API runs under a permissive `"ui"` authorization policy —
  the single place to require OIDC JWT bearer auth later, without touching endpoints. pantheon
  has route/HTTP plumbing ready for an OIDC login.

## Prerequisites

- .NET 10 SDK
- Node 22+ (Angular 19)
- Docker (for SQL Server 2025 locally) — or a reachable SQL Server instance

## Run the dev stack

```bash
# SQL Server 2025 + styx + pantheon
docker compose -f deploy/docker-compose.yml up -d --build
```

styx applies EF migrations on startup (`Database:MigrateOnStartup`). Then open
<http://localhost:4200>.

### Run pieces directly (without Docker for the apps)

```bash
# 1. SQL Server only
docker compose -f deploy/docker-compose.yml up -d sqlserver

# 2. styx (REST :8080, gRPC :8081)
dotnet run --project src/Argus.Styx

# 3. pantheon dev server (http://localhost:4200)
cd src/pantheon && npx ng serve

# 4. herald — point it at a folder of *.log files and at styx
#    edit src/Argus.Herald/appsettings.json: Herald.LogPaths, ApiKey, StyxGrpcEndpoint
dotnet run --project src/Argus.Herald
```

## Installing the agent on a host

- **Linux:** `sudo deploy/herald/install-linux.sh` (publishes self-contained, installs a
  systemd unit `argus-herald`).
- **Windows:** run `deploy/herald/install-windows.ps1` elevated (publishes self-contained,
  registers the `ArgusHerald` Windows Service).
- **macOS:** `sudo deploy/herald/install-macos.sh` (publishes self-contained, installs a
  launchd daemon `com.argus.herald`).

### Windows / IIS (styx + pantheon)

See [deploy/iis/README.md](deploy/iis/README.md) — pantheon runs as an IIS site, styx runs as
a Kestrel Windows Service that IIS reverse-proxies (gRPC isn't supported behind IIS). The UI
supports light/dark mode (toggle in the top bar; defaults to the OS preference, persisted per browser).

Edit the installed `appsettings.json` (`StyxGrpcEndpoint`, `ApiKey`, `LogPaths`) and restart.

## EF migrations

```bash
# create a migration
dotnet ef migrations add <Name> --project src/Argus.Codex
# apply to a running SQL Server (or let styx do it on startup)
ARGUS_CONNECTION="Server=localhost,1433;Database=Argus;User Id=sa;Password=Your_password123;TrustServerCertificate=True" \
  dotnet ef database update --project src/Argus.Codex
```

## Status / roadmap

Implemented: CPU/RAM/disk metrics, per-process snapshots, multi-path log tailing with restart-safe
checkpoints, gRPC ingest with API-key auth, REST + SignalR query API, retention purge, and the
pantheon dashboard + log viewer.

Planned: OS event-log collection (Windows Event Viewer / journald) end-to-end, OIDC auth, mTLS
option for agents, and time-series partitioning if volume grows.
