# Radio Link Visualization — Full Refactoring Plan

## Goal

Replace the existing MapLibre-only map rendering with a hybrid architecture:
- **MapLibre GL JS** — static base map only (Positron tiles, 3D buildings, contour lines)
- **deck.gl** — all dynamic data (nodes, labels, radio links, channel cohort lines, activity pulses)

Add two new toggleable persistent map layers:
1. **Radio links** — real RF adjacency from decoded `NEIGHBORINFO_APP` (portnum 71) packets, colored by SNR
2. **Channel cohorts** — persistent same-channel pair lines (promoting the current hover-only behavior)

Both toggles are off by default and persist to localStorage.

---

## Architecture Overview

```
MapLibre GL JS (base map)
  └── Positron style tiles (OpenFreeMap)
  └── fill-extrusion 3D buildings (zoom ≥ 15)
  └── Contour lines (maplibre-contour + DEM tiles)

deck.gl MapboxOverlay (synchronized WebGL overlay)
  └── ArcLayer        — radio links (SNR-gradient, toggleable)
  └── ArcLayer        — channel cohort links (toggleable)
  └── ScatterplotLayer — nodes (battery-colored circles)
  └── TextLayer        — node labels (hover/pinned only)
  └── ScatterplotLayer — activity pulses (rAF-animated)
```

### Why deck.gl for dynamic data

| Concern | MapLibre GeoJSON layers | deck.gl |
|---|---|---|
| Node rendering | Circle layer + GeoJSON source | `ScatterplotLayer` with per-object accessors |
| Labels | Symbol layer with filter | `TextLayer` with `background: true` |
| Radio links | Line layer (no gradient) | `ArcLayer` with `getSourceColor`/`getTargetColor` (gradient per arc) |
| Hover/click | `mousemove`/`mouseleave` on layer ID | `onHover`/`onClick` callbacks on layer |
| Atomic update | Multiple `source.setData()` calls | Single `deck.setProps({ layers: [...] })` |
| Performance at scale | GeoJSON re-serialization on every change | WebGL buffer updates only |
| Pulse animation | `source.setData()` on rAF | `deck.setProps()` on rAF |

---

## Libraries

| Library | CDN | Purpose |
|---|---|---|
| `maplibre-gl@^5` | `unpkg.com/maplibre-gl@^5/dist/maplibre-gl.js` | Base map |
| `maplibre-contour@0.0.9` | `unpkg.com/maplibre-contour@0.0.9/dist/index.min.js` | Contour lines |
| `deck.gl` (standalone) | `unpkg.com/deck.gl@^9/dist.min.js` | Dynamic data layers |

deck.gl standalone bundle exposes `deck` global with `MapboxOverlay`, `ArcLayer`, `ScatterplotLayer`, `TextLayer`.

---

## Data Pipeline — NeighborInfo Packets

### What Meshtastic provides

`NEIGHBORINFO_APP` (portnum 71) packets are broadcast periodically by each node. Payload (protobuf):

```proto
message Neighbor {
  fixed32 node_id      = 1;
  float   snr          = 2;   // IEEE 754, encoded as fixed32 (wire type 5)
  fixed32 last_rx_time = 3;   // Unix timestamp
}

message NeighborInfo {
  fixed32  node_id                    = 1;  // reporting node
  fixed32  last_sent_by_id            = 2;
  uint32   node_broadcast_interval_secs = 3;
  repeated Neighbor neighbors         = 4;
}
```

Field numbers must be verified against the official Meshtastic protobufs at implementation time.

### Client-side vs server-side

This feature is **client-side only** for the initial implementation. The client-side Web Worker (`realtimePacketWorkerCore.mjs`) already decodes packets from raw MQTT payloads. NeighborInfo will be decoded there and projected into a new `RadioLinkProjectionStore` in the browser.

Server-side persistence (Phase 6) is optional and deferred.

### Link staleness

Links not refreshed within **2 hours** are evicted on the next `Project()` call. The staleness window is a named constant in `RadioLinkProjectionStore`.

### Bidirectional deduplication

Canonical key: `min(sourceId, targetId) + "|" + max(sourceId, targetId)`. If Node A reports B and Node B reports A, the most recent observation wins and only one line is drawn. SNR from the newer report is used; if one direction has SNR and the other does not, prefer the non-null value.

---

## Phase 1 — Protobuf and Server-Side Decoding

### 1.1 Extend `meshtastic_subset.proto`

**File**: `src/MeshBoard.Infrastructure.Meshtastic/Protos/meshtastic_subset.proto`

- Add `NEIGHBORINFO_APP = 71` to the `PortNum` enum
- Add `Neighbor` message type (fields: `node_id`, `snr`, `last_rx_time`)
- Add `NeighborInfo` message type (fields: `node_id`, `last_sent_by_id`, `node_broadcast_interval_secs`, `neighbors`)

### 1.2 Add `MeshtasticNeighborEntry` contract

**File (new)**: `src/MeshBoard.Contracts/Meshtastic/MeshtasticNeighborEntry.cs`

```csharp
public sealed class MeshtasticNeighborEntry
{
    public string NodeId { get; set; } = string.Empty;
    public float? SnrDb { get; set; }
    public DateTimeOffset? LastRxAtUtc { get; set; }
}
```

### 1.3 Extend `MeshtasticEnvelope`

**File**: `src/MeshBoard.Contracts/Meshtastic/MeshtasticEnvelope.cs`

- Add `List<MeshtasticNeighborEntry>? Neighbors { get; set; }`

### 1.4 Decode NeighborInfo in `MeshtasticEnvelopeReader`

**File**: `src/MeshBoard.Infrastructure.Meshtastic/Decoding/MeshtasticEnvelopeReader.cs`

- Add `case PortNum.NeighborinfoApp` in `BuildPayloadPreview`
- Add `DecodeNeighborInfoPayload` method: parse protobuf, populate `envelope.Neighbors`, return `"Neighbor info: N neighbors reported"`
- Add `"Neighbor Info"` to `GetPacketType`

---

## Phase 2 — Client-Side Web Worker Decoding

### 2.1 Parse NeighborInfo in `realtimePacketWorkerCore.mjs`

**File**: `src/MeshBoard.Client/wwwroot/js/realtimePacketWorkerCore.mjs`

- Add `neighborinfoApp: 71` to `portNums`
- Add entry in `packetTypesByPortNum` for port 71
- Add `case portNums.neighborinfoApp` branch in the packet decode switch
- Implement `parseNeighborInfoPayload(bytes)`:
  - Field 1 (wire type 5, fixed32): `node_id` of the reporting node
  - Field 4 (wire type 2, length-delimited): repeated `Neighbor` sub-messages
  - Each `Neighbor`: field 1 (fixed32) `node_id`, field 2 (fixed32 IEEE 754) `snr` via `DataView.getFloat32(offset, true)`, field 3 (fixed32) `last_rx_time`
- Attach parsed result as `neighborInfo: { reportingNodeId, neighbors: [{ nodeId, snrDb, lastRxTime }] }` on the decoded packet object

**Risk**: The `snr` field is a float encoded as `fixed32` (wire type 5). Must use `DataView.getFloat32` with little-endian flag. Validate with known test payloads.

### 2.2 Create `RealtimeNeighborInfoEvent`

**File (new)**: `src/MeshBoard.Client/Realtime/RealtimeNeighborInfoEvent.cs`

```csharp
public sealed class RealtimeNeighborInfoEvent
{
    public string ReportingNodeId { get; init; } = string.Empty;
    public IReadOnlyList<RealtimeNeighborEntry> Neighbors { get; init; } =
        Array.Empty<RealtimeNeighborEntry>();
}

public sealed class RealtimeNeighborEntry
{
    public string NodeId { get; init; } = string.Empty;
    public float? SnrDb { get; init; }
    public DateTimeOffset? LastRxAtUtc { get; init; }
}
```

### 2.3 Extend `RealtimeDecodedPacketEvent`

**File**: `src/MeshBoard.Client/Realtime/RealtimeDecodedPacketEvent.cs`

- Add `RealtimeNeighborInfoEvent? NeighborInfo { get; init; }`

---

## Phase 3 — Client-Side Radio Link Projection Store

### 3.1 `RadioLinkEnvelope`

**File (new)**: `src/MeshBoard.Client/Maps/RadioLinkEnvelope.cs`

```csharp
public sealed record RadioLinkEnvelope(
    string SourceNodeId,
    string TargetNodeId,
    float? SnrDb,
    DateTimeOffset LastSeenAtUtc
);
```

### 3.2 `RadioLinkProjectionStore`

**File (new)**: `src/MeshBoard.Client/Maps/RadioLinkProjectionStore.cs`

- Internal `Dictionary<string, RadioLinkEnvelope>` keyed by canonical `min|max` node ID pair
- `void Project(RealtimePacketWorkerResult)` — upserts neighbor entries, evicts stale links, fires `Changed`
- `IReadOnlyList<RadioLinkEnvelope> Current` — current active links
- `void Clear()`
- `event Action? Changed`
- Staleness constant: `private static readonly TimeSpan StalenessWindow = TimeSpan.FromHours(2)`

**Deduplication logic**: on upsert, compute canonical key. If entry exists and incoming `LastSeenAtUtc` is newer, replace. Prefer non-null SNR when merging.

### 3.3 `RadioLinkPoint` — JS-serializable DTO

**File (new)**: `src/MeshBoard.Client/Maps/RadioLinkPoint.cs`

```csharp
public sealed record RadioLinkPoint(
    string SourceNodeId,
    string TargetNodeId,
    float? SnrDb,
    double SourceLatitude,
    double SourceLongitude,
    double TargetLatitude,
    double TargetLongitude
);
```

Coordinates resolved at render time by cross-referencing `MapProjectionStore.Current.Nodes`.

### 3.4 `MapLayerVisibility`

**File (new)**: `src/MeshBoard.Client/Maps/MapLayerVisibility.cs`

```csharp
public sealed record MapLayerVisibility(
    bool RadioLinks = false,
    bool ChannelCohorts = false
);
```

### 3.5 Register in DI

**File**: `src/MeshBoard.Client/Program.cs`

```csharp
builder.Services.AddScoped<RadioLinkProjectionStore>();
```

### 3.6 Wire into `BrowserRealtimeClient`

**File**: `src/MeshBoard.Client/Realtime/BrowserRealtimeClient.cs`

- Inject `RadioLinkProjectionStore`
- After `_mapProjectionStore.Project(packet)`, call `_radioLinkProjectionStore.Project(packet)`

---

## Phase 4 — JavaScript: deck.gl Overlay + All Dynamic Layers

This phase is the largest change. `node-map.js` is rewritten to use deck.gl for all dynamic data.

### 4.1 Load deck.gl (lazy, alongside MapLibre)

Add to `index.html`:
```html
<script src="https://unpkg.com/deck.gl@^9/dist.min.js"></script>
```

In `ensureMapLibre()`, also ensure `window.deck` is available before proceeding.

### 4.2 Remove MapLibre dynamic layers and sources

The following are **removed** from `node-map.js`:
- `NODE_SOURCE_ID`, `NODE_LAYER_ID`, `NODE_LABEL_LAYER_ID`
- `LINK_SOURCE_ID`, `LINK_LAYER_ID` (hover links)
- `PULSE_SOURCE_ID`, `PULSE_LAYER_ID`
- `addMapSources()` — replaced by `addDeckLayers()`
- `addMapLayers()` — replaced by `addDeckLayers()`
- `wireInteractions()` — replaced by deck.gl `onHover`/`onClick` callbacks
- `refreshNodeAppearance()` — replaced by `updateDeckLayers()`
- `buildNodeGeoJson()`, `buildLinkGeoJson()` — replaced by direct data arrays

Kept in MapLibre:
- `addBuildingsLayer()` (fill-extrusion)
- `addContourLayers()` (maplibre-contour)
- `findFirstLabelLayerId()` helper

### 4.3 Map state: add `deck` instance

The `mapState` object gains:
```js
{
  map,              // MapLibre map
  deck,             // deck.gl MapboxOverlay instance
  nodeDataById,     // Map<nodeId, node>
  hoveredNodeId,
  pinnedNodeId,
  didAutoFrame,
  activePulses,
  rafId,
  layerVisibility,  // { radioLinks: bool, channelCohorts: bool }
  radioLinks,       // RadioLinkPoint[]
}
```

### 4.4 Deck layer builders

#### `buildScatterplotLayer(nodes, hoveredNodeId)` — nodes

```js
new deck.ScatterplotLayer({
  id: 'meshboard-nodes',
  data: nodes,
  getPosition: d => [d.longitude, d.latitude],
  getRadius: d => d.nodeId === hoveredNodeId ? 9 : 7,
  radiusUnits: 'pixels',
  radiusMinPixels: 4,
  radiusMaxPixels: 16,
  getFillColor: d => hexToRgba(resolveBatteryFill(d.batteryLevelPercent)),
  getLineColor: d => hexToRgba(
    d.nodeId === hoveredNodeId
      ? resolveChannelColorHex(d.channel)
      : resolveBatteryStroke(d.batteryLevelPercent)
  ),
  stroked: true,
  lineWidthUnits: 'pixels',
  getLineWidth: d => d.nodeId === hoveredNodeId ? 3 : 2,
  pickable: true,
  autoHighlight: false,
  onHover: info => handleNodeHover(mapState, info),
  onClick: info => handleNodeClick(mapState, info),
  updateTriggers: { ... }
})
```

#### `buildTextLayer(nodes, hoveredNodeId)` — labels

```js
new deck.TextLayer({
  id: 'meshboard-node-labels',
  data: nodes.filter(n => n.nodeId === hoveredNodeId),
  getPosition: d => [d.longitude, d.latitude],
  getText: d => `${d.displayName}\n${d.channel ? 'Channel ' + d.channel : 'Channel unknown'}`,
  getSize: 12,
  getColor: [247, 239, 225],
  fontFamily: 'ui-sans-serif, system-ui, sans-serif',
  fontWeight: 600,
  background: true,
  getBackgroundColor: [16, 32, 47, 210],
  getBorderColor: [16, 32, 47, 0],
  getBorderWidth: 0,
  getPixelOffset: [0, -18],
  getTextAnchor: 'middle',
  getAlignmentBaseline: 'bottom',
  pickable: false,
})
```

#### `buildRadioLinkLayer(radioLinks, visible)` — radio links

```js
new deck.ArcLayer({
  id: 'meshboard-radio-links',
  data: radioLinks,
  visible,
  getSourcePosition: d => [d.sourceLongitude, d.sourceLatitude],
  getTargetPosition: d => [d.targetLongitude, d.targetLatitude],
  getSourceColor: d => snrToRgba(d.snrDb),
  getTargetColor: d => snrToRgba(d.snrDb),
  getWidth: d => snrToWidth(d.snrDb),
  getHeight: 0,           // flat on map, no 3D arc
  widthUnits: 'pixels',
  widthMinPixels: 1,
  widthMaxPixels: 4,
  opacity: 0.7,
  pickable: true,
  onHover: info => handleLinkHover(mapState, info),
})
```

SNR color mapping:
```
snrDb < -10  →  [227,  74,  51, 200]  (red,    weak)
snrDb  0     →  [253, 187, 132, 200]  (amber,  moderate)
snrDb > 5    →  [ 49, 163,  84, 200]  (green,  strong)
snrDb null   →  [160, 160, 160, 160]  (gray,   unknown)
```

#### `buildChannelCohortLayer(nodes, visible)` — channel cohorts

```js
new deck.ArcLayer({
  id: 'meshboard-channel-cohorts',
  data: buildChannelCohortPairs(nodes),  // [{source, target, color}]
  visible,
  getSourcePosition: d => [d.sourceLongitude, d.sourceLatitude],
  getTargetPosition: d => [d.targetLongitude, d.targetLatitude],
  getSourceColor: d => [...hexToRgb(d.color), 120],
  getTargetColor: d => [...hexToRgb(d.color), 120],
  getWidth: 1,
  getHeight: 0,
  widthUnits: 'pixels',
  opacity: 0.55,
  pickable: false,
})
```

`buildChannelCohortPairs(nodes)`: group by channel, generate all pairs within each channel. Cap at 300 pairs per channel, selecting nearest pairs first (using existing `distanceBetweenNodes`). Total cap: 1000 pairs globally.

#### `buildPulseLayer(activePulses)` — activity pulses

```js
new deck.ScatterplotLayer({
  id: 'meshboard-pulses',
  data: activePulses.map(p => ({
    position: [p.node.longitude, p.node.latitude],
    radius: computePulseRadius(p),
    color: hexToRgba(p.color, computePulseOpacity(p)),
  })),
  getPosition: d => d.position,
  getRadius: d => d.radius,
  radiusUnits: 'pixels',
  getFillColor: d => d.color,
  stroked: false,
  pickable: false,
})
```

### 4.5 `addDeckLayers(mapState)` — initialization

```js
function addDeckLayers(mapState) {
  const overlay = new deck.MapboxOverlay({
    interleaved: false,  // overlay above MapLibre, not interleaved
    layers: buildAllDeckLayers(mapState),
  });

  mapState.map.addControl(overlay);
  mapState.deck = overlay;
}
```

### 4.6 `updateDeckLayers(mapState)` — called on every data change

```js
function updateDeckLayers(mapState) {
  mapState.deck.setProps({
    layers: buildAllDeckLayers(mapState),
  });
}

function buildAllDeckLayers(mapState) {
  const activeNodeId = mapState.pinnedNodeId ?? mapState.hoveredNodeId;
  const nodes = Array.from(mapState.nodeDataById.values());

  return [
    buildRadioLinkLayer(mapState.radioLinks, mapState.layerVisibility.radioLinks),
    buildChannelCohortLayer(nodes, mapState.layerVisibility.channelCohorts),
    buildPulseLayer(mapState.activePulses),
    buildScatterplotLayer(nodes, activeNodeId),
    buildTextLayer(nodes, activeNodeId),
  ];
}
```

Layer order (bottom to top): radio links → channel cohorts → pulses → nodes → labels.

### 4.7 Interaction handlers

```js
function handleNodeHover(mapState, info) {
  const nodeId = info.object?.nodeId ?? null;
  if (nodeId === mapState.hoveredNodeId) return;
  mapState.hoveredNodeId = nodeId;
  mapState.map.getCanvas().style.cursor = nodeId ? 'pointer' : 'grab';
  updateDeckLayers(mapState);
  mapState.dotNetCallbackRef?.invokeMethodAsync('OnNodeHoveredFromMap', nodeId);
}

function handleNodeClick(mapState, info) {
  const nodeId = info.object?.nodeId ?? null;
  mapState.pinnedNodeId = mapState.pinnedNodeId === nodeId ? null : nodeId;
  mapState.hoveredNodeId = nodeId;
  updateDeckLayers(mapState);
  mapState.dotNetCallbackRef?.invokeMethodAsync('OnNodeSelectedFromMap', nodeId);
}

function handleLinkHover(mapState, info) {
  // Show/hide a MapLibre popup with link details
  // or update a Blazor-side panel via invokeMethodAsync
}
```

### 4.8 Update `renderNodeMap` signature

```js
export async function renderNodeMap(
  containerId,
  nodes,
  activityPulses,
  fitCameraToNodes,
  dotNetCallbackRef,
  radioLinks,           // new: RadioLinkPoint[]
  layerVisibility       // new: { radioLinks: bool, channelCohorts: bool }
)
```

- Normalize and store `radioLinks` on `mapState`
- Store `layerVisibility` on `mapState`
- Call `updateDeckLayers(mapState)` after updating state
- Bump module version to `?v=4`

### 4.9 Mini map

The mini map (`renderMiniMap`) uses MapLibre only (no deck.gl). The single node dot stays as a MapLibre circle layer. No change needed — mini map is read-only and doesn't need deck.gl overhead.

### 4.10 Pulse animation

The rAF loop calls `updateDeckLayers(mapState)` instead of `source.setData()`. Same timing, same eviction logic — only the output changes.

---

## Phase 5 — Blazor UI Integration

### 5.1 Map.razor — inject and subscribe

**File**: `src/MeshBoard.Client/Pages/Map.razor`

- `@inject RadioLinkProjectionStore RadioLinkProjectionStore`
- In `OnInitialized`: `RadioLinkProjectionStore.Changed += HandleProjectionStoreChanged`
- In `DisposeAsync`: `RadioLinkProjectionStore.Changed -= HandleProjectionStoreChanged`
- In `ClearMapCache`: also call `RadioLinkProjectionStore.Clear()`

### 5.2 Map.razor — new state fields

```csharp
private bool _showRadioLinks;
private bool _showChannelCohorts;
private IReadOnlyList<RadioLinkPoint> _radioLinkPoints = Array.Empty<RadioLinkPoint>();
```

### 5.3 Map.razor — build radio link points

In `RefreshView()`, after building `_visibleMapNodes`:

```csharp
_radioLinkPoints = RadioLinkProjectionStore.Current
    .Select(link => TryResolveCoordinates(link, _visibleNodes))
    .OfType<RadioLinkPoint>()
    .ToArray();
```

Where `TryResolveCoordinates` returns null if either endpoint has no coordinates.

### 5.4 Map.razor — update `RenderMapAsync` call

```csharp
await module.InvokeVoidAsync(
    "renderNodeMap",
    MapElementId,
    _visibleMapNodes,
    _pendingActivityPulses,
    _shouldFitMapToNodes,
    _mapCallbackReference,
    _radioLinkPoints,
    new { radioLinks = _showRadioLinks, channelCohorts = _showChannelCohorts });
```

### 5.5 Map.razor — toggle controls

Add two toggle buttons alongside "Refresh map" / "Reset view" in the top overlay `div`:

```html
<button type="button"
        class="@GetToggleClass(_showRadioLinks)"
        @onclick="ToggleRadioLinksAsync">
    Radio links @(_radioLinkPoints.Count > 0 ? $"({_radioLinkPoints.Count})" : "")
</button>
<button type="button"
        class="@GetToggleClass(_showChannelCohorts)"
        @onclick="ToggleChannelCohortsAsync">
    Channel cohort
</button>
```

Toggle handlers:
```csharp
private async Task ToggleRadioLinksAsync()
{
    _showRadioLinks = !_showRadioLinks;
    await JS.InvokeVoidAsync("localStorage.setItem", "meshboard:map:radioLinks",
        _showRadioLinks.ToString().ToLower());
    _isMapRenderPending = true;
    StateHasChanged();
}
```

Restore from localStorage in `OnAfterRenderAsync(firstRender: true)`:
```csharp
_showRadioLinks = await JS.InvokeAsync<string>(
    "localStorage.getItem", "meshboard:map:radioLinks") == "true";
_showChannelCohorts = await JS.InvokeAsync<string>(
    "localStorage.getItem", "meshboard:map:channelCohorts") == "true";
```

### 5.6 Map.razor — SNR legend

When `_showRadioLinks` is true, render a small legend below the focused node panel:

```html
@if (_showRadioLinks)
{
    <div class="pointer-events-none absolute bottom-7 left-7 z-10 ...">
        <div class="shell-subpanel rounded-[1.35rem] p-3 text-xs space-y-1.5">
            <p class="font-semibold uppercase tracking-[0.14em] text-cinder-800/62">SNR legend</p>
            <div class="flex items-center gap-2">
                <span class="h-2 w-6 rounded-full bg-[#31a354]"></span><span>Strong (> 0 dB)</span>
            </div>
            <div class="flex items-center gap-2">
                <span class="h-2 w-6 rounded-full bg-[#fdbb84]"></span><span>Moderate</span>
            </div>
            <div class="flex items-center gap-2">
                <span class="h-2 w-6 rounded-full bg-[#e34a33]"></span><span>Weak (< -10 dB)</span>
            </div>
            <div class="flex items-center gap-2">
                <span class="h-2 w-6 rounded-full bg-[#a0a0a0]"></span><span>Unknown</span>
            </div>
        </div>
    </div>
}
```

### 5.7 Map.razor — update stats panel

Add radio link count alongside existing node/position counts:
```html
<p>Radio links: @_radioLinkPoints.Count</p>
```

### 5.8 Map.razor — update description text

```
Browse all locally projected nodes on the map. Click a node to pin focus,
click the map again to release it. Toggle Radio links to see real RF adjacency
from NeighborInfo packets. Toggle Channel cohort to see same-channel connections.
3D buildings visible at close zoom.
```

---

## Phase 6 — Server-Side Persistence (Deferred)

Optional, not required for initial implementation. Enables historical link data and cross-session persistence.

### 6.1 `neighbor_links` table

```sql
CREATE TABLE IF NOT EXISTS neighbor_links (
    workspace_id        TEXT    NOT NULL,
    source_node_id      TEXT    NOT NULL,
    target_node_id      TEXT    NOT NULL,
    snr_db              REAL    NULL,
    reported_by_node_id TEXT    NOT NULL,
    last_seen_at_utc    TEXT    NOT NULL,
    PRIMARY KEY (workspace_id, source_node_id, target_node_id)
);
CREATE INDEX IF NOT EXISTS ix_neighbor_links_workspace_seen
    ON neighbor_links (workspace_id, last_seen_at_utc DESC);
```

### 6.2 `INeighborLinkRepository`

**File**: `src/MeshBoard.Application/Abstractions/Persistence/INeighborLinkRepository.cs`

```csharp
public interface INeighborLinkRepository
{
    Task UpsertAsync(string workspaceId, IReadOnlyList<NeighborLinkRecord> links,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NeighborLinkRecord>> GetActiveLinksAsync(
        string workspaceId, TimeSpan maxAge,
        CancellationToken cancellationToken = default);
}
```

### 6.3 Extend `MeshtasticIngestionService`

**File**: `src/MeshBoard.Application/Services/MeshtasticIngestionService.cs`

- Inject `INeighborLinkRepository`
- In `IngestEnvelope`: if `envelope.Neighbors` is populated, upsert via repository

---

## Files Changed Summary

### New files

| Path | Description |
|---|---|
| `src/MeshBoard.Client/Maps/RadioLinkEnvelope.cs` | Immutable record for one radio link |
| `src/MeshBoard.Client/Maps/RadioLinkProjectionStore.cs` | Client-side store for neighbor link graph |
| `src/MeshBoard.Client/Maps/RadioLinkPoint.cs` | JS-serializable DTO with resolved coordinates |
| `src/MeshBoard.Client/Maps/MapLayerVisibility.cs` | Toggle state record |
| `src/MeshBoard.Client/Realtime/RealtimeNeighborInfoEvent.cs` | Decoded NeighborInfo event model |
| `src/MeshBoard.Contracts/Meshtastic/MeshtasticNeighborEntry.cs` | Server-side neighbor entry DTO |

### Modified files

| Path | Change |
|---|---|
| `src/MeshBoard.Infrastructure.Meshtastic/Protos/meshtastic_subset.proto` | Add NeighborInfo + Neighbor messages, NEIGHBORINFO_APP portnum |
| `src/MeshBoard.Infrastructure.Meshtastic/Decoding/MeshtasticEnvelopeReader.cs` | Decode NeighborInfo packets |
| `src/MeshBoard.Contracts/Meshtastic/MeshtasticEnvelope.cs` | Add `Neighbors` property |
| `src/MeshBoard.Client/wwwroot/js/realtimePacketWorkerCore.mjs` | Add NeighborInfo packet parsing |
| `src/MeshBoard.Client/Realtime/RealtimeDecodedPacketEvent.cs` | Add `NeighborInfo` property |
| `src/MeshBoard.Client/Realtime/BrowserRealtimeClient.cs` | Wire `RadioLinkProjectionStore.Project()` |
| `src/MeshBoard.Client/Program.cs` | Register `RadioLinkProjectionStore` |
| `src/MeshBoard.Client/Pages/Map.razor` | Toggle UI, radio link data, updated JS call |
| `src/MeshBoard.Client/wwwroot/js/node-map.js` | Full rewrite of dynamic layers to deck.gl |
| `src/MeshBoard.Client/wwwroot/index.html` | Add deck.gl CDN script |

---

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| NeighborInfo packets disabled on most nodes in the mesh | Medium | Show "No radio links reported yet" when store is empty. The feature is non-destructive — zero links means zero lines drawn. |
| `snr` field is a float in fixed32 encoding, easy to mis-parse in the JS worker | Medium | Use `DataView.getFloat32(offset, true)` (little-endian). Validate with known protobuf test vectors. |
| deck.gl bundle size (~1.5 MB min+gzip) increasing initial load | Low-Medium | Load lazily alongside MapLibre (already done). deck.gl CDN has good cache hit rates. |
| Channel cohort pairwise O(n²) with large channels | Low | Cap at 300 pairs per channel, 1000 total. Select nearest pairs first. |
| `Map.razor` exceeding 800-line guideline | Medium | Extract toggle controls + SNR legend into `MapLayerControls.razor` sub-component if needed. |
| Bidirectional link deduplication losing asymmetric SNR data | Low | Accepted trade-off for v1. Document and defer per-direction SNR to a future enhancement. |
| deck.gl `interleaved: false` rendering above MapLibre popups | Low | Use `interleaved: true` if z-ordering issues arise (requires MapLibre WebGL2 support). |

---

## Success Criteria

- [ ] NeighborInfo packets (portnum 71) are decoded in both the server-side reader and the JS Web Worker
- [ ] Radio links appear on the map as flat arcs colored by SNR when the toggle is enabled
- [ ] Channel cohort links appear as persistent arcs when the toggle is enabled
- [ ] Both toggles persist across page refreshes via localStorage
- [ ] Stale radio links (> 2 hours) are automatically evicted
- [ ] Bidirectional links are deduplicated to one arc
- [ ] Null SNR renders as gray with "Unknown" in tooltip
- [ ] All node rendering (circles, labels, pulses) moved to deck.gl layers
- [ ] MapLibre retains only buildings and contour lines
- [ ] Existing hover/pin/channel focus behavior preserved with identical UX
- [ ] Mini map unaffected (still MapLibre-only)
- [ ] Map performs acceptably with 1000+ radio link arcs
