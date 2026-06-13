# Windows / IIS deployment

This sets up Argus on a Windows host: the **pantheon** web UI in IIS, and **styx** as a
Kestrel Windows Service that IIS reverse-proxies to.

## Why styx is a Windows Service, not an IIS site

styx exposes a **gRPC** endpoint that the herald agents stream to. **ASP.NET Core gRPC
servers are not supported behind IIS** (IIS does not support the HTTP/2 response trailers
gRPC requires — this is a documented Microsoft limitation). So styx runs directly on Kestrel
as a Windows Service, and IIS is used for the static SPA plus a reverse proxy for the REST
and SignalR traffic. Agents connect to the gRPC port on Kestrel directly.

## Topology

```
                          ┌─────────────────────────── Windows host ───────────────────────────┐
  Browser ──HTTP──▶ IIS site (pantheon, :80) ──reverse proxy /api, /hubs──▶ styx service :8080
                          │                                                  (REST + SignalR)   │
  herald agents ─────────────────────── gRPC :8081 ───────────────────────▶ styx service :8081 │
                          └─────────────────────────────────────────────────────────────────────┘
                                                          │
                                                          ▼
                                                   SQL Server (Argus DB)
```

- IIS publicly serves the SPA on :80 and proxies `/api` + `/hubs/*` to styx on `localhost:8080`,
  so the browser sees a single origin (no CORS needed; pantheon's production build uses relative URLs).
- herald agents reach gRPC on `:8081` directly (not through IIS).

## Prerequisites (on the IIS host)

- **.NET 10** — not required if you publish styx self-contained (the script does). Otherwise
  install the .NET 10 Hosting Bundle / runtime.
- **IIS** with these modules/features:
  - **URL Rewrite** module
  - **Application Request Routing (ARR)** module — and enable proxy:
    IIS Manager → server node → *Application Request Routing Cache* → *Server Proxy Settings* → check **Enable proxy**.
  - **WebSocket Protocol** Windows feature (for SignalR).
- **Node.js** to build the SPA (or build it elsewhere and copy `dist/`).
- **SQL Server** reachable from the host (see the connection string parameter).

## Steps (run elevated PowerShell)

```powershell
# 1. styx as a Windows Service (Kestrel) — pass your SQL connection string
.\install-styx-service.ps1 -ConnectionString "Server=SQLHOST;Database=Argus;User Id=argus;Password=...;TrustServerCertificate=True"

# 2. pantheon as an IIS site (defaults to port 80)
.\install-pantheon-iis.ps1 -Port 80
```

Then browse to `http://<host>/`.

## Security checklist before going live

- Replace the default `Ingest:ApiKeys` value in styx `appsettings.json` with real per-agent keys.
- Use a least-privilege SQL login (not `sa`) in the connection string.
- Put IIS behind HTTPS (bind a certificate to the site); the proxy rule forwards `X-Forwarded-Proto`.
- Restrict who can reach the gRPC port (`:8081`) to your agent network.

## Updating

Re-run either script; both replace the existing service/site in place.
