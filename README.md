# MeshBoard

Browser-first Meshtastic MQTT monitoring built around:

- `MeshBoard.Client` for the WebAssembly UI, local key vault, browser-side decryption, and local projections
- `MeshBoard.Api` for cookie auth, preferences, realtime-session bootstrap, and VerneMQ webhook auth
- `MeshBoard.RealtimeBridge` for upstream MQTT consume and downstream republish

`MeshBoard.Web` has been removed. The active local product stack is the compose-based client/API/bridge runtime under `ops/local/`.

## Active Architecture

- Browser UI: `src/MeshBoard.Client`
- API: `src/MeshBoard.Api`
- HTTP SDK: `src/MeshBoard.Api.SDK`
- Realtime bridge: `src/MeshBoard.RealtimeBridge`
- Collector: `src/MeshBoard.Collector`
- Shared contracts: `src/MeshBoard.Contracts`
- Application services: `src/MeshBoard.Application`
- Persistence: `src/MeshBoard.Infrastructure.Persistence`
- Meshtastic transport and decode infrastructure: `src/MeshBoard.Infrastructure.Meshtastic`

## Local Development

Prerequisites:

- .NET SDK 10.x
- Node.js 20+ and npm
- Docker Engine with Compose support
- `openssl`

Install JS dependencies and build the client stylesheet:

```bash
npm ci
npm run tailwind:build
```

Bootstrap local secrets and broker certificates:

```bash
./ops/local/generate-local-stack-secrets.sh
```

Start the active local stack:

```bash
cp ops/local/.env.example ops/local/.env.local
docker compose --env-file ops/local/.env.local -f ops/local/compose.yaml up --build
```

Useful endpoints:

- Client: `http://localhost:8082`
- API health: `http://localhost:8081/api/health`
- PostgreSQL: `postgresql://meshboard:meshboard@localhost:15432/meshboard`
- VerneMQ WS: `ws://localhost:8080/mqtt`
- VerneMQ WSS: `wss://localhost:8084/mqtt`

For more details, see [ops/local/README.md](/home/armeldemarsac/Documents/Personnal/Development/Projects/School/Virus/Server/ops/local/README.md).

## Build

Build the solution:

```bash
dotnet build MeshBoard.slnx
```

## Tests

```bash
dotnet test tests/MeshBoard.UnitTests/MeshBoard.UnitTests.csproj
dotnet test tests/MeshBoard.IntegrationTests/MeshBoard.IntegrationTests.csproj
```

Optional live decode smoke test against real Meshtastic MQTT traffic:

```bash
MESHBOARD_LIVE_DECODE_SMOKE=1 \
MESHBOARD_LIVE_DECODE_SECONDS=25 \
MESHBOARD_LIVE_DECODE_KEYS="AQ==" \
dotnet test tests/MeshBoard.UnitTests/MeshBoard.UnitTests.csproj --filter LiveDecodeSmoke
```

Set `MESHBOARD_LIVE_DECODE_REQUIRE_TEXT=1` to fail the smoke test unless at least one text message is decoded.

## Docs

- Current repo/runtime picture: [docs/PROJECT_FOUNDATIONS.md](/home/armeldemarsac/Documents/Personnal/Development/Projects/School/Virus/Server/docs/PROJECT_FOUNDATIONS.md)
- Collector traffic schema: [docs/COLLECTOR_POSTGRES_SCHEMA.md](/home/armeldemarsac/Documents/Personnal/Development/Projects/School/Virus/Server/docs/COLLECTOR_POSTGRES_SCHEMA.md)
- Broad migration history: [docs/ARCHITECTURE_REFACTOR_ROADMAP.md](/home/armeldemarsac/Documents/Personnal/Development/Projects/School/Virus/Server/docs/ARCHITECTURE_REFACTOR_ROADMAP.md)
- Current cleanup sequence: [docs/CLIENT_FIRST_CLEANUP_PLAN.md](/home/armeldemarsac/Documents/Personnal/Development/Projects/School/Virus/Server/docs/CLIENT_FIRST_CLEANUP_PLAN.md)
