# Local Compose Stack

This stack launches the active browser-first runtime locally:

- `meshboard-client`
- `postgres`
- `meshboard-api`
- `meshboard-vernemq`
- `meshboard-realtime-bridge`

`meshboard-client` is an edge container. It serves the published Blazor WebAssembly app and proxies same-origin `/api/*` and `/.well-known/*` requests to `meshboard-api`, so the current cookie-auth and antiforgery model keeps working without a cross-origin client rewrite.

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
cp ops/local/.env.example ops/local/.env.local
docker compose --env-file ops/local/.env.local -f ops/local/compose.yaml up --build
```

## Optional Cloudflare Tunnel

You can expose the same-origin client edge for remote testing by enabling the optional `cloudflared` profile.
Only `meshboard-client` should be exposed. The client edge already proxies `/api/*` to `meshboard-api` and `/mqtt`
to VerneMQ, so the public hostname stays same-origin for the browser.

Before starting the tunnel profile, export:

```bash
cp ops/local/.env.example ops/local/.env.local
```

Edit `ops/local/.env.local`:

```dotenv
CF_TUNNEL_TRANSPORT_PROTOCOL=http2
CF_TUNNEL_TOKEN=your-tunnel-token
MESHBOARD_USE_REQUEST_ORIGIN_BROKER_URL=true
MESHBOARD_BROKER_PATH=/mqtt
MESHBOARD_ALLOW_INSECURE_BROKER_URL=false
```

Then start:

```bash
docker compose --env-file ops/local/.env.local -f ops/local/compose.yaml --profile tunnel up --build -d
```

Notes:

- Do not commit the tunnel token.
- If your network blocks outbound UDP to Cloudflare, set `CF_TUNNEL_TRANSPORT_PROTOCOL=http2` to avoid QUIC handshake delays.
- Keep `MESHBOARD_USE_REQUEST_ORIGIN_BROKER_URL=true` so the API emits `wss://<public-host>/mqtt` automatically from the incoming request host.
- `MESHBOARD_PUBLIC_BROKER_URL` is only needed if you intentionally want to override that derived broker URL.
- The browser should use the public hostname, for example `https://your-public-hostname.example.com`.

Useful endpoints:

- Client: `http://localhost:8082`
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
