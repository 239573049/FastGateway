# FastGateway Management Console

<p align="center">
  <img src="docs/images/hero.jpg" alt="FastGateway control plane" width="100%">
</p>

FastGateway is a self-hosted reverse-proxy and tunnel gateway. It combines a JWT-protected management API, a React dashboard, dynamically managed HTTP/HTTPS gateways, TCP/UDP L4 forwarding, TunnelClient-based access to private services, and optional Master/Worker clustering.

-----
Document Language: [English](README.md) | [简体中文](README-zh-cn.md)

See [CHANGELOG.md](CHANGELOG.md) for release notes.

<p align="center">
  <img src="docs/images/dashboard.jpg" alt="FastGateway dashboard" width="100%">
</p>

## Architecture

FastGateway is a single deployable control plane and proxy runtime. `src/FastGateway` hosts the management API and dashboard, and creates one in-process Kestrel/YARP gateway for each enabled `Server` configuration. `TunnelClient` is an independent outbound agent for private services. Multiple FastGateway instances can form a cluster: the Master owns configuration, Workers apply snapshots locally and can relay traffic to a designated access node.

```text
                                   ┌─────────────────────────────┐
 Browser / clients ──HTTP(S)/H2/H3►│ FastGateway                  │
                                   │ management host              │
                                   │ JWT API + React dashboard    │
                                   └──────────────┬──────────────┘
                                                  │
        ┌──────────────────────────┬──────────────┼──────────────────────────┐
        ▼                          ▼              ▼                          ▼
 per-Server Kestrel + YARP   Tunnel manager   L4 stream manager        Cluster
 domain/path routes          HTTP/2 CONNECT   TCP / UDP / Both         Master / Worker
 service/cluster/static/     / WebSocket      upstream pool            config sync +
 tunnel / access-node        ⇄ TunnelClient                            data-plane relay
        │                          │              │                          │
        ▼                          ▼              ▼                          ▼
 HTTP services and files     local services   TCP/UDP services         peer gateways
```

### Source layout

| Path | Responsibility |
| --- | --- |
| `src/FastGateway` | Main ASP.NET Core host, JWT API, static dashboard hosting, dynamic HTTP gateways, certificates, access control, rate limiting, statistics, file storage, and tunnel management. |
| `src/FastGateway/Gateway` | Per-server YARP route/cluster construction and the TCP/UDP `StreamProxyManager`. |
| `src/FastGateway/Services` | Minimal API endpoint groups and configuration-backed business services. |
| `src/FastGateway/Middleware` | Client-IP resolution, statistics, timeout, access-control, failover, proxy-error, and abnormal-IP middleware. |
| `src/FastGateway/Tunnels` | Server-side tunnel registration, agent lifecycle, control channels, and per-request tunnel streams. |
| `src/FastGateway/Cluster` | Master/Worker clustering: invite/join, WebSocket config sync, MessagePack protocol, and data-plane relay. |
| `src/TunnelClient` | Standalone tunnel agent. Reads `tunnel.json`, connects to the gateway over HTTP/2 or WebSocket, and forwards traffic to local services through YARP. |
| `src/Core` | Shared stream wrappers and gateway entities/enums used by the server and agent. |
| `src/Certes` | Vendored, Newtonsoft.Json/BouncyCastle-free, AOT-compatible ACME client used for Let's Encrypt certificates. |
| `web` | React 19 + TypeScript + Vite dashboard. Docker and release builds copy its `dist` output into `src/FastGateway/wwwroot`. |

### Runtime flow

1. `Program` initializes `FastGatewayOptions`, JWT authentication, background services, `ConfigurationService`, and `ClusterStateService`.
2. The configuration service loads `data/gateway.config` (creating it when absent) and atomically writes changes back to disk. Cluster role and node membership live in `data/cluster.json`.
3. Enabled `Server` records create independent Kestrel/YARP gateway instances. Enabled `StreamForward` records start TCP/UDP listeners in `StreamProxyManager`.
4. Each domain route is compiled into an in-memory YARP route and cluster. A route can target one service, a service cluster, a local static-file root, or a registered tunnel node (`node_<name>`). In a cluster, a route may also name an access node so other gateways relay instead of connecting upstream themselves.
5. Gateway middleware resolves the client IP, handles ACME HTTP-01 challenges and HTTPS redirects, collects statistics, applies timeouts/rate limits/blacklists, performs failover and error handling, then forwards through YARP.
6. The explicit reload APIs and tunnel registration update the in-memory route provider without requiring a process restart. On a Master, configuration changes are debounced and pushed to online Workers.

### Tunnel data path

<p align="center">
  <img src="docs/images/tunnel.jpg" alt="FastGateway private tunnel" width="100%">
</p>

`TunnelClient` is started with `-c <config-file>`, registers its node with `/internal/gateway/Server/register`, and maintains a control connection to `/internal/gateway/Server`. The transport type is `h2` (HTTP/2 CONNECT) or `ws` (WebSocket); both use the `FastGateway` sub-protocol. When a public request matches a tunnel route, the server allocates a tunnel ID, the agent opens the corresponding data stream, and the two sides copy bytes bidirectionally to the local service.

### Management API surface

The backend uses ASP.NET Core Minimal APIs. The main groups are `/api/v1/authorization`, `/server`, `/domain`, `/cert`, `/tunnel`, `/stream-forward`, `/cluster`, `/black-and-white`, `/rate-limit`, `/abnormal-ip`, `/statistics`, `/qps`, `/filestorage`, `/setting`, and `/system`. Most management groups require the JWT issued by `POST /api/v1/authorization`. Cluster join/register/sync endpoints authenticate with invite or node tokens rather than JWT.

### Runtime data

All paths below are relative to the application directory; in the Docker image that directory is `/app`.

| Path | Purpose |
| --- | --- |
| `data/gateway.config` | Persistent gateway, domain, certificate, access-control, rate-limit, setting, and L4 forwarding configuration. |
| `data/cluster.json` | Cluster role, invite tokens, and Master/Worker membership. |
| `data/stats.db` | SQLite request-statistics database used by the statistics background service. |
| `data/keys/` | ACME account keys cached by email for certificate renewal. |
| `certs/` | Generated or uploaded PFX certificates selected by SNI. |
| `gateway.pfx` | Packaged fallback certificate used when no matching certificate is available. |
| `ip2region.xdb` | Optional offline IP geolocation database used for traffic analysis. |

## Supported Features

- [x] Login authorization
- [x] Automatic HTTPS certificate application (Let's Encrypt / HTTP-01)
- [x] Automatic HTTPS certificate renewal
- [x] Wildcard domain certificates (Let's Encrypt / DNS-01)
- [x] Upload custom HTTPS certificates (PFX / PEM)
- [x] Dashboard monitoring
- [x] Static file service (ETag / Last-Modified, 304)
- [x] Single service proxy
- [x] Cluster proxy
- [x] TCP/UDP L4 port forwarding
- [x] TunnelClient-based private service forwarding
- [x] Master/Worker clustering with config sync and traffic relay
- [x] Per-route access node (which cluster member reaches the upstream)
- [x] Per-server client IP source (`X-Forwarded-For` / `X-Real-IP` / `CF-Connecting-IP`)
- [x] Upstream health checks and request-level failover
- [x] Traffic statistics and IP geolocation
- [x] Request source analysis
- [x] Configurable request-log retention (1 / 7 / 15 / 30 days)
- [x] Custom rate-limit policies (per-IP fixed window)
- [x] Black and white lists
- [x] Abnormal-IP detection
- [x] In-process file manager with chunked upload

## Cluster Management

<p align="center">
  <img src="docs/images/cluster.jpg" alt="FastGateway Master/Worker cluster" width="100%">
</p>

Open **Cluster** in the dashboard. A new instance starts as **Standalone**. Clustering is optional: a single node keeps working exactly as before.

1. **Create a Master** — On the gateway that should own configuration, enter a management URL that Workers can reach (for example `https://gw-a.example.com:8080`) and generate an invite code. The code is valid for 24 hours. Generating the first invite promotes the node to Master.

2. **Join as a Worker** — On another FastGateway, paste the invite code, optionally set a node name, and join. The Worker registers with the Master, then keeps a WebSocket control channel open. The Master immediately pushes a full configuration snapshot (servers, domain routes, black/white lists, rate-limit policies, L4 forwards, certificates, and PFX files). Later edits on the Master are pushed automatically; local Worker edits are overwritten on the next sync.

3. **Access node** — On a domain route, the Master can choose which member should reach the upstream: the receiving node (default), the Master, or a named Worker. Requests that land on a different member are relayed (Worker → Master over the Master's business port; Master → Worker over an outbound cluster tunnel; Worker → other Worker in two hops via the Master).

4. **Operations** — The Master shows node online status and synced version, and can push config immediately, remove a node, or dissolve the cluster. A Worker can leave and keep the last synced config as a standalone gateway. Workers do not run ACME issuance or renewal; certificates are issued on the Master and distributed with the snapshot.

The node-to-node protocol is WebSocket binary frames: 1-byte version + MessagePack with LZ4 compression, serialized by a source-generated resolver so Native AOT stays reflection-free.

## HTTPS Certificate Management

FastGateway supports three ways to provide HTTPS certificates for your domains, all managed from the **Certificate Management** page:

1. **Automatic (Let's Encrypt, HTTP-01)** — For a normal domain (e.g. `example.com`), add the domain and email, then click **Apply**. The gateway completes the ACME HTTP-01 challenge automatically (an enabled port-80 service is required) and renews the certificate before it expires.

2. **Wildcard domain (Let's Encrypt, DNS-01)** — For a wildcard domain (e.g. `*.example.com`), HTTP-01 cannot be used. Add the domain, then click **DNS Verify**: the gateway generates a `_acme-challenge` TXT record for you to add at your DNS provider. Once the record has propagated, click **Verify & Issue** to complete the challenge. Wildcard certificates issued this way are not auto-renewed — re-run the DNS verification before they expire.

3. **Upload your own certificate** — Click **Upload Certificate** to use a certificate you already own. Two formats are supported:
   - **PFX / P12** — upload the `.pfx`/`.p12` file together with its password (leave empty if it has none).
   - **PEM / CRT** — upload the certificate (`.pem`/`.crt`) and its private key (`.key`) separately.

   Uploaded certificates (including wildcard ones) are matched by SNI and do not participate in automatic renewal; upload a new file before expiry. Uploaded certificates are converted to and stored as `.pfx` under the `certs` directory.

## Technology Stack

### Backend

- .NET 10 and ASP.NET Core Minimal APIs
- Kestrel with HTTP/1.1, HTTP/2, and HTTP/3 support for configured HTTPS gateways
- YARP 2.3 for reverse proxy routing, clusters, health checks, and forwarding
- JWT Bearer authentication for the management API
- JSON file persistence for gateway configuration (`data/gateway.config`) and cluster state (`data/cluster.json`)
- Microsoft.Data.Sqlite for request statistics (`data/stats.db`)
- `System.Threading.RateLimiting` for per-IP partitioned fixed-window rate limits
- MessagePack (LZ4) for AOT-safe cluster snapshots and certificate file transfer
- Certes ACME client for Let's Encrypt HTTP-01/DNS-01 certificate workflows
- IP2Region.Net and `ip2region.xdb` for offline IP attribution

### Frontend

- React 19, TypeScript, and Vite
- React Router for dashboard routing and lazy-loaded pages
- Ant Design and Radix UI for controls and interaction primitives
- Tailwind CSS v4 for styling
- ECharts/Recharts for traffic and dashboard visualizations

## Quick Start

The container uses port `8080` for the management UI/API. HTTP/HTTPS listener ports are opened by enabled `Server` records in the dashboard.

```bash
mkdir -p data certs

docker run -d --restart=always --name=fast-gateway \
  --user 0:0 \
  -e PASSWORD='change-this-password' \
  -e TunnelToken='change-this-tunnel-token' \
  -p 8080:8080 \
  -p 80:80/tcp \
  -p 443:443/tcp \
  -p 443:443/udp \
  -v "$(pwd)/data:/app/data" \
  -v "$(pwd)/certs:/app/certs" \
  aidotnet/fast-gateway:latest
```

Open `http://localhost:8080` after the container starts. If no password is supplied, a random initial password is generated and saved to `data/admin-password` (the path is also logged at startup); set `PASSWORD` before exposing the management endpoint. `443/udp` is required for HTTP/3.

The published image is a Native AOT build on a chiseled base (no shell). The base image defaults to UID 1654; this repo's image and Compose run as `user: "0:0"` so bind-mounted `data`/`certs` stay writable. `aidotnet/fast-gateway` is a multi-arch image for `linux/amd64` and `linux/arm64`.

## Docker Compose

The checked-in `docker-compose.yml` builds `src/FastGateway/Dockerfile`, persists `data` and `certs`, maps the management endpoint to port `8000`, and tags the image as `registry.cn-shenzhen.aliyuncs.com/token-ai/fast-gateway`:

```bash
docker compose -f docker-compose.yml up -d --build
```

It maps `8000:8080`, `80:80`, and both `443/tcp` and `443/udp` for HTTP/3. For a pre-built multi-architecture image, set `image: aidotnet/fast-gateway:latest` and remove the `build` section. Set `PASSWORD` and `TunnelToken` in the Compose `environment` for production deployments.

## Using `systemd` to Start Services on Linux

Download the Linux archive from [Releases](../../releases), extract it (for example to `/opt/fastgateway`), then create `fastgateway.service`:

```shell
nano /etc/systemd/system/fastgateway.service
```

Replace paths and secrets in the unit file:

```ini
[Unit]
Description=FastGateway

[Service]
WorkingDirectory=/opt/fastgateway
ExecStart=/opt/fastgateway/FastGateway
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=dotnet-fastgateway
User=root
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=PASSWORD=change-this-password
Environment=TunnelToken=change-this-tunnel-token

[Install]
WantedBy=multi-user.target
```

Reload systemd and start the service:

```shell
systemctl daemon-reload
systemctl start fastgateway.service
systemctl enable fastgateway.service
systemctl status fastgateway.service
```

Stop or restart with `systemctl stop fastgateway.service` and `systemctl restart fastgateway.service`.

## Development and Build

Requirements: .NET 10 SDK, Node.js 20+ with npm, and the native toolchain required by the target build. The default project settings enable Native AOT analysis/publishing for the main applications; use `-p:PublishAot=false` when producing a portable single-file artifact as the release workflow does.

```bash
# Build the .NET solution
dotnet build FastGateway.sln

# Build the dashboard
cd web
npm install
npm run build
cd ..

# Run the management host with the launch profile (http://localhost:5202)
dotnet run --project src/FastGateway/FastGateway.csproj

# Run a tunnel client; -c is required by the client entry point
dotnet run --project src/TunnelClient/TunnelClient.csproj -- -c ./src/TunnelClient/tunnel.json
```

For dashboard-only development, run `npm run dev` in `web`; Vite proxies `/api` to the backend at `http://localhost:5202`. Docker builds the dashboard in a Node 22 stage, then publishes the FastGateway Native AOT image for `linux/amd64` and `linux/arm64`. The Release workflow publishes self-contained single-file FastGateway and TunnelClient archives for Linux, Windows, and macOS on x64 and ARM64.

## Downloads

Pre-built, self-contained binaries are published on the [Releases](../../releases) page for the following platforms:

| OS | x64 | ARM64 |
| --- | --- | --- |
| Linux | `fastgateway-linux-x64.tar.gz` | `fastgateway-linux-arm64.tar.gz` |
| Windows | `fastgateway-win-x64.zip` | `fastgateway-win-arm64.zip` |
| macOS | `fastgateway-osx-x64.tar.gz` | `fastgateway-osx-arm64.tar.gz` |

The `TunnelClient` component is published for the same set of platforms (`tunnelclient-<runtime>` archives).

The Docker image `aidotnet/fast-gateway` is a multi-arch image supporting both `linux/amd64` and `linux/arm64`, so `docker run`/`docker compose` automatically pulls the right variant for your host.

## Third-Party Downloads

- [ip2region.xdb](https://tokenfile.oss-cn-beijing.aliyuncs.com/ip2region.xdb) for offline IP attribution
