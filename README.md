# MeshBoard

Blazor Web App (`.NET 10`) for Meshtastic MQTT monitoring.

It connects to `mqtt.meshtastic.org`, lets you subscribe to channel topics, browse nodes, inspect message traffic, manage favorites, and view node/channel details.

## Tech Stack

- .NET 10 (Blazor Web App, Interactive Server)
- SQLite + Dapper persistence
- MQTTnet + Meshtastic protobuf decoding
- Tailwind CSS v4

## Run Locally

Prerequisites:

- .NET SDK 10.x
- Node.js 20+ and npm

Commands:

```bash
npm ci
npm run tailwind:build
dotnet run --project src/MeshBoard.Web/MeshBoard.Web.csproj
```

App URL: `http://localhost:5222` (or the port shown in terminal output).

For live CSS updates:

```bash
npm run tailwind:watch
```

Run that in a second terminal while running the app.

## Tests

```bash
dotnet test tests/MeshBoard.UnitTests/MeshBoard.UnitTests.csproj
dotnet test tests/MeshBoard.IntegrationTests/MeshBoard.IntegrationTests.csproj
```

## Docker

Build image:

```bash
docker build -t meshboard:latest .
```

Run container:

```bash
docker run --rm -p 8080:8080 \
  -e Persistence__ConnectionString="Data Source=/data/meshboard.db" \
  -v meshboard-data:/data \
  meshboard:latest
```

App URL: `http://localhost:8080`

## Common Environment Overrides

- `Broker__Host`
- `Broker__Port`
- `Broker__Username`
- `Broker__Password`
- `Broker__DefaultTopicPattern`
- `Broker__DefaultEncryptionKeyBase64`
- `Broker__EnableSend`
- `Persistence__ConnectionString`

Notes:

- Default broker credentials in `appsettings.json` target the public Meshtastic MQTT server.
- `Broker__EnableSend` is `false` by default.
