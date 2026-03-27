# MeshBoard

Browser-first Meshtastic MQTT monitoring built around:

- `MeshBoard.Client` for the WebAssembly UI, local key vault, browser-side decryption, and local projections
- `MeshBoard.Api` for cookie auth, preferences, realtime-session bootstrap, and VerneMQ webhook auth
- `MeshBoard.RealtimeBridge` for upstream MQTT consume and downstream republish
- An event-driven collector pipeline (Kafka) for large-scale packet ingestion, normalization, and graph/stats projection

`MeshBoard.Web` has been removed. `MeshBoard.Collector` has been retired in favour of the new event-driven pipeline. The active local product stack is the compose-based runtime under `ops/local/`.

## Active Architecture

### Product runtime

- Browser UI: `src/MeshBoard.Client`
- API: `src/MeshBoard.Api`
- HTTP SDK: `src/MeshBoard.Api.SDK`
- Realtime bridge: `src/MeshBoard.RealtimeBridge`

### Event-driven collector pipeline

| Worker | Source project | Role |
|--------|---------------|------|
| Ingress | `src/MeshBoard.Collector.Ingress` | Subscribes to upstream MQTT and publishes `RawPacketReceived` to Kafka |
| Normalizer | `src/MeshBoard.Collector.Normalizer` | Decodes and decrypts raw packets, publishes `NodeObserved`, `LinkObserved`, `TelemetryObserved`, `PacketNormalized` |
| Stats projector | `src/MeshBoard.Collector.StatsProjector` | Upserts hourly rollup rows in PostgreSQL (TimescaleDB hypertables) |
| Graph projector | `src/MeshBoard.Collector.GraphProjector` | Writes nodes and links into Neo4j |
| Topology analyst | `src/MeshBoard.Collector.TopologyAnalyst` | Runs scheduled GDS graph projections and community detection in Neo4j |

### Shared libraries

- Shared contracts and events: `src/MeshBoard.Contracts`
- Application services: `src/MeshBoard.Application`
- Persistence (PostgreSQL + TimescaleDB): `src/MeshBoard.Infrastructure.Persistence`
- Graph persistence (Neo4j): `src/MeshBoard.Infrastructure.Neo4j`
- Kafka eventing infrastructure: `src/MeshBoard.Infrastructure.Eventing`
- Meshtastic transport and decode: `src/MeshBoard.Infrastructure.Meshtastic`

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

Start the product stack (client, API, bridge, VerneMQ, PostgreSQL, Neo4j):

```bash
cp ops/local/.env.example ops/local/.env.local
docker compose --env-file ops/local/.env.local -f ops/local/compose.yaml up --build
```

To also run the full event-driven collector pipeline (adds Kafka and all five collector workers), enable the `collector-v2` profile:

```bash
docker compose --env-file ops/local/.env.local -f ops/local/compose.yaml --profile collector-v2 up --build
```

Useful endpoints:

- Client: `http://localhost:8082`
- API health: `http://localhost:8081/api/health`
- PostgreSQL (TimescaleDB): `postgresql://meshboard:meshboard@localhost:15432/meshboard`
- Neo4j browser: `http://localhost:7474` (user: `neo4j`, password: `meshboard`)
- Kafka: `localhost:9092`
- VerneMQ WS: `ws://localhost:8080/mqtt`
- VerneMQ WSS: `wss://localhost:8084/mqtt`

For more details, see [ops/local/README.md](ops/local/README.md).

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

- Current repo/runtime picture: [docs/PROJECT_FOUNDATIONS.md](docs/PROJECT_FOUNDATIONS.md)
- Event-driven collector plan: [docs/COLLECTOR_EVENT_DRIVEN_PLAN.md](docs/COLLECTOR_EVENT_DRIVEN_PLAN.md)
- Collector traffic schema: [docs/COLLECTOR_POSTGRES_SCHEMA.md](docs/COLLECTOR_POSTGRES_SCHEMA.md)
- Broad migration history: [docs/ARCHITECTURE_REFACTOR_ROADMAP.md](docs/ARCHITECTURE_REFACTOR_ROADMAP.md)
