# Realtime Load Tests

This project provides the Phase 5 load/concurrency harness for the browser-to-broker realtime path.

## Scenarios

- `connect-burst`
  Opens many short-lived browser-style MQTT over `WSS` sessions concurrently, subscribes to the authorized topic filter, holds them for a configurable duration, then disconnects.
- `reconnect-storm`
  Repeats session issuance and reconnect cycles across many clients to measure token-refresh and reconnect pressure.

## Run

```bash
dotnet run --project src/MeshBoard.RealtimeLoadTests/MeshBoard.RealtimeLoadTests.csproj
```

Configuration comes from `appsettings.json` and standard environment overrides.

Examples:

```bash
RealtimeLoadTests__ApiBaseUrl=https://api.meshboard.local \
RealtimeLoadTests__Username=meshboard-loadtest \
RealtimeLoadTests__Password=password-123 \
RealtimeLoadTests__Scenario=connect-burst \
RealtimeLoadTests__ClientCount=250 \
RealtimeLoadTests__MaxConcurrency=50 \
dotnet run --project src/MeshBoard.RealtimeLoadTests/MeshBoard.RealtimeLoadTests.csproj
```

```bash
RealtimeLoadTests__Scenario=reconnect-storm \
RealtimeLoadTests__ClientCount=100 \
RealtimeLoadTests__ReconnectIterations=5 \
RealtimeLoadTests__DelayBetweenReconnectMilliseconds=100 \
dotnet run --project src/MeshBoard.RealtimeLoadTests/MeshBoard.RealtimeLoadTests.csproj
```

## Output

Each run writes a JSON report under `artifacts/realtime-load-tests/`.

Track at least:

- session issuance latency
- connect latency
- reconnect latency
- success rate
- failure reasons

Use those reports to satisfy the Phase 5 benchmark and reconnect-storm gates in `docs/ARCHITECTURE_REFACTOR_ROADMAP.md`.
