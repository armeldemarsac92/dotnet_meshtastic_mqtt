# Local Compose Stack

This stack launches the server-side Phase 5 runtime locally:

- `postgres`
- `meshboard-api`
- `meshboard-vernemq`
- `meshboard-realtime-bridge`

It intentionally does **not** containerize `MeshBoard.Client` yet. The current client still assumes a same-origin host, and that deserves its own gateway/proxy slice instead of a rushed cross-origin workaround.

## Why The VerneMQ Image Is Built Locally

The stack builds VerneMQ from the open-source source tarball instead of using the prebuilt Docker image path that downloads a binary package under EULA. That keeps this repo aligned with the project's strict self-hosted open-source requirement.

## Prerequisites

- Docker Engine with Compose support
- `openssl`

## Bootstrap Secrets And Certificates

Run:

```bash
./ops/local/generate-local-stack-secrets.sh
```

That generates:

- `ops/local/secrets/realtime-signing-private-key.pem`
- `ops/vernemq/certs/ca.pem`
- `ops/vernemq/certs/tls.crt`
- `ops/vernemq/certs/tls.key`

## Start The Stack

```bash
docker compose -f ops/local/compose.yaml up --build
```

Useful endpoints:

- API health: `http://localhost:8081/api/health`
- PostgreSQL: `postgresql://meshboard:meshboard@localhost:15432/meshboard`
- VerneMQ TCP: `mqtt://localhost:1883`
- VerneMQ WS: `ws://localhost:8080/mqtt`
- VerneMQ WSS: `wss://localhost:8084/mqtt`

## Stop And Remove

```bash
docker compose -f ops/local/compose.yaml down -v
```

## Browser And Load-Test Trust

The generated VerneMQ certificate is signed by the local CA at `ops/vernemq/certs/ca.pem`. For browser-based testing and local load tests against `wss://localhost:8084/mqtt`, import that CA into the machine trust store first. Without that, the browser and MQTT clients will reject the self-signed broker certificate.
