# MeshBoard UX Restructure Plan

## Goal

Simplify the mental model: topics and channels are the same concept. Remove the duplication,
subscribe to the Meshtastic broker root automatically, and reorganize the UI around the 5
things users actually do.

---

## Mental Model (after)

```
Server  = where you connect (broker: host, port, credentials, downlink topic)
Channel = what you watch (discovered automatically from the packet stream)
Message = what flows through a channel
```

No "topic presets". No "saved channel filters". Channel configuration lives per-channel
as a popup. Encryption keys are browser-only (client Vault).

---

## Architecture Context

The browser does NOT connect directly to the Meshtastic MQTT broker. The relay is:

```
Meshtastic MQTT broker
       ↓  (backend subscribes with msh/# automatically)
MeshBoard API (decodes + relays packets)
       ↓  (republishes to internal topic)
VerneMQ  (meshboard/workspaces/{id}/live/#)
       ↓  (browser subscribes here via WebSocket)
Browser client
```

---

## Phase 1 — Backend Cleanup (removals, no new features)

### What to remove
- API endpoint group: `GET /api/preferences/topic-presets`, `POST /api/preferences/topic-presets`
- API endpoint group: `GET /api/preferences/channels`, `POST /api/preferences/channels`, `DELETE /api/preferences/channels`
- Services: `ProductTopicPresetPreferenceService`, `SavedChannelPreferenceService`
- Repositories: `ProductTopicPresetRepository`, `SavedChannelFilterRepository`
- Interfaces: `IProductTopicPresetPreferenceService`, `ISavedChannelPreferenceService`, `ITopicPresetRepository`, `ISavedChannelFilterRepository`
- SQL query classes: `TopicPresetQueries`, `SavedChannelFilterQueries`
- SQL response models: `TopicPresetSqlResponse`, `SavedChannelFilterSqlResponse`
- SQL request models: `UpsertTopicPresetSqlRequest`
- `TopicPresetEncryptionKeyResolver` (backend decryption via topic preset key — client Vault handles this)
- Contracts/DTOs: all files in `Contracts/Topics/` related to TopicPreset and SavedChannelFilter
- DI registrations for all removed services/repos
- Endpoint mappings: `MapTopicPresetPreferenceEndpoints`, `MapChannelPreferenceEndpoints`

### What to modify
- `ResolveDesiredTopicFiltersAsync` in `LocalBrokerRuntimeCommandService`:
  Replace multi-source union with hardcoded `msh/#` (plus the companion json filter expansion)
- `broker_server_profiles` table: drop `default_topic_pattern`, `default_encryption_key_base64`
  KEEP: `downlink_topic`, `enable_send` (send/publish feature remains)
- `SavedBrokerServerProfile`, `BrokerServerProfile`, `SaveBrokerPreferenceRequest` contracts:
  Remove `DefaultTopicPattern`, `DefaultEncryptionKeyBase64`
  Keep `DownlinkTopic`, `EnableSend`
- Database migrations: add migration to drop `topic_presets` table, `saved_channel_filters` table,
  and the two columns from `broker_server_profiles`
- SQLite schema initializer: remove topic preset seeding and migration

### Database tables
- DROP: `topic_presets`
- DROP: `saved_channel_filters`
- ALTER `broker_server_profiles`: DROP COLUMN `default_topic_pattern`, DROP COLUMN `default_encryption_key_base64`

---

## Phase 2 — Frontend Navigation Restructure

### New route structure (5 pages)
| Route | Page | Was |
|-------|------|-----|
| `/` | Overview (Dashboard) | unchanged |
| `/feed` | Live Feed | `/messages` |
| `/nodes` | Nodes + Map toggle | `/nodes` (expanded) |
| `/channels` | Channel Browser | `/channels` (right panel only) |
| `/settings` | Settings | `/settings` (expanded) |

### Removed routes (with redirects)
- `/messages` → redirect to `/feed`
- `/map` → redirect to `/nodes`
- `/favorites` → redirect to `/settings`

### Navigation changes
- Sidebar order: Overview, Feed, Nodes, Channels, Settings
- Bottom nav (mobile, 5 tabs): Overview | Feed | Nodes | Channels | Settings
  (no more "More" sheet — everything fits in 5 tabs)
- Post-login redirect: `/` instead of `/favorites`

### Overview page (Dashboard) simplification
Remove: decrypt health section, full session details, workspace technical metadata
Add: persistent connection status card at top (broker name + status dot + Connect/Disconnect)
Keep: 3 summary metric cards (active nodes, messages received, active channels)
Keep: compact recent activity (5 messages + 5 most active nodes)

---

## Phase 3 — Feed Page (was Messages)

### Channel filter tabs
Replace the old "receive scope" section with a horizontal scrollable chip bar at the top:
```
[All]  [Favorites]  [LongFast]  [MedFast]  [PrivateChannel]  ...
```
- "All" = no channel filter, show everything
- "Favorites" = only channels marked as favorite
- Channel chips appear as channels are discovered (dynamic)
- Selected chip shows a compact channel info bar below with a settings gear icon
- Settings gear → per-channel popup (Phase 4)

### Connection controls
Move Connect/Disconnect controls from a buried section to a compact sticky header within this page.
Show: status dot + broker name + button. Always visible when on Feed page.

---

## Phase 4 — Per-Channel Popup Settings

When clicking the settings icon on a channel chip (or a channel card in the Channels page):

Popup contains:
- Display name / label (editable)
- Encryption key (stored in browser Vault, never sent to server)
- Favorite toggle (star)
- Packet count, last seen (read-only info)

Storage: browser LocalStorage via existing Vault infrastructure.
No API calls needed for channel settings — entirely client-side.

---

## Phase 5 — Nodes Page with Map Toggle + Favorites Filter

### Map integration
- Add [List | Map] toggle button in the Nodes page header
- List view = current nodes list
- Map view = current /map globe (Cesium integration, channel cohort legend, click-to-pin)
- Both views share the same search/filter state

### Favorites as filter
- Add filter chips: [All] [Favorites] [Has Location] [Has Telemetry]
- Favorites chip filters to starred nodes (replaces /favorites page entirely)
- Star toggle on each node card/row syncs via existing API (FavoritePreferenceApiClient)

---

## Phase 6 — Settings Expansion

New Settings sections/tabs:
- **Broker** — connection details: Name, Host, Port, TLS, Username, Password, Downlink Topic, Enable Send
  (DefaultTopicPattern removed, broker creation is now much simpler)
- **Favorites** — node favorites CRUD (moved from /favorites page)
- **Vault** — encryption keys health, local storage info
- **Account** — username, logout

Removed from Settings:
- Topics / Presets tab (entire concept removed)
- Channel Filters tab (entire concept removed)

---

## Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Map Cesium JS interop in conditional render | Medium | Test in Nodes page, lazy-load JS |
| Route bookmark breakage (/messages, /map, /favorites) | Low | Add redirects |
| Existing users have topic presets configured — data loss on migration | Medium | Migration drops tables; document breaking change |
| Backend subscribes to msh/# — higher traffic volume | Low | No user impact; backend already handles all packets |
| Channel settings are browser-only — lost on clear site data | Low | Acceptable for MVP; note in UI |

---

## Status

- [x] Phase 1 — Backend cleanup
- [x] Phase 2 — Frontend navigation restructure
- [x] Phase 3 — Feed page with channel tabs
- [x] Phase 4 — Per-channel popup settings
- [x] Phase 5 — Nodes page with map toggle
- [x] Phase 6 — Settings expansion
