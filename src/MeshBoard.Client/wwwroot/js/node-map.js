const MAPLIBRE_SCRIPT_ID = "meshboard-maplibre-script";
const MAPLIBRE_SCRIPT_URL = "https://unpkg.com/maplibre-gl@^5/dist/maplibre-gl.js";
const STYLE_URL = "https://tiles.openfreemap.org/styles/positron";
const DEM_TILES_URL = "https://demotiles.maplibre.org/terrain-tiles/{z}/{x}/{y}.png";
const HOVER_LINK_LIMIT = 18;
const DEFAULT_CENTER = [0, 20];
const DEFAULT_ZOOM = 2;
const NODE_SOURCE_ID = "meshboard-nodes";
const LINK_SOURCE_ID = "meshboard-links";
const PULSE_SOURCE_ID = "meshboard-pulse";
const CONTOUR_SOURCE_ID = "meshboard-contours";
const NODE_LAYER_ID = "meshboard-node-circles";
const NODE_LABEL_LAYER_ID = "meshboard-node-labels";
const LINK_LAYER_ID = "meshboard-link-lines";
const BUILDINGS_LAYER_ID = "meshboard-buildings";
const PULSE_LAYER_ID = "meshboard-pulse-circles";
const CONTOUR_LAYER_ID = "meshboard-contour-lines";
const MINI_NODE_SOURCE_ID = "meshboard-mini-node";
const MINI_CONTOUR_SOURCE_ID = "meshboard-mini-contours";
const MINI_NODE_LAYER_ID = "meshboard-mini-node-circle";
const MINI_BUILDINGS_LAYER_ID = "meshboard-mini-buildings";
const MINI_CONTOUR_LAYER_ID = "meshboard-mini-contour-lines";

let maplibreLoadPromise = null;
let demSource = null;
const mapStates = new Map();
const miniMapStates = new Map();

function ensureMapLibreScript() {
    const existing = document.getElementById(MAPLIBRE_SCRIPT_ID);

    if (existing) {
        if (window.maplibregl) {
            return Promise.resolve();
        }

        return new Promise((resolve, reject) => {
            existing.addEventListener("load", () => resolve(), { once: true });
            existing.addEventListener("error", () => reject(new Error("Failed to load MapLibre GL JS.")), { once: true });
        });
    }

    return new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.id = MAPLIBRE_SCRIPT_ID;
        script.src = MAPLIBRE_SCRIPT_URL;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error("Failed to load MapLibre GL JS."));
        document.head.appendChild(script);
    });
}

async function ensureMapLibre() {
    if (window.maplibregl && demSource) {
        return;
    }

    if (!maplibreLoadPromise) {
        maplibreLoadPromise = (async () => {
            await ensureMapLibreScript();

            if (!demSource && window.mlcontour) {
                demSource = new window.mlcontour.DemSource({
                    url: DEM_TILES_URL,
                    encoding: "mapbox",
                    maxzoom: 12,
                    worker: true
                });
                demSource.setupMaplibre(window.maplibregl);
            }
        })();
    }

    await maplibreLoadPromise;
}

function resolveBatteryColor(batteryLevelPercent) {
    if (!Number.isFinite(batteryLevelPercent)) {
        return { stroke: "#2f6ea8", fill: "#6bb6ef" };
    }

    if (batteryLevelPercent <= 15) {
        return { stroke: "#b23c3c", fill: "#ef8787" };
    }

    if (batteryLevelPercent <= 35) {
        return { stroke: "#ba7041", fill: "#f3a96f" };
    }

    if (batteryLevelPercent <= 60) {
        return { stroke: "#9d8a34", fill: "#ebd37c" };
    }

    return { stroke: "#317e49", fill: "#73c98f" };
}

function resolveChannelColorHex(channel) {
    if (!channel) {
        return "#6bb6ef";
    }

    let hash = 0;
    for (let index = 0; index < channel.length; index += 1) {
        hash = ((hash << 5) - hash) + channel.charCodeAt(index);
        hash |= 0;
    }

    const hue = Math.abs(hash) % 360;
    return hslToHex(hue, 72, 58);
}

function hslToHex(h, s, l) {
    const sl = s / 100;
    const ll = l / 100;
    const a = sl * Math.min(ll, 1 - ll);

    function channel(n) {
        const k = (n + h / 30) % 12;
        const color = ll - a * Math.max(Math.min(k - 3, 9 - k, 1), -1);
        return Math.round(255 * color).toString(16).padStart(2, "0");
    }

    return `#${channel(0)}${channel(8)}${channel(4)}`;
}

function normalizeNodes(nodes) {
    return (nodes ?? [])
        .map((node) => ({
            nodeId: node.nodeId,
            displayName: node.displayName ?? node.nodeId ?? "Unknown node",
            channel: node.channel ?? null,
            latitude: Number.parseFloat(node.latitude),
            longitude: Number.parseFloat(node.longitude),
            batteryLevelPercent: Number.isFinite(Number.parseFloat(node.batteryLevelPercent))
                ? Number.parseFloat(node.batteryLevelPercent)
                : Number.NaN
        }))
        .filter((node) => Number.isFinite(node.latitude) && Number.isFinite(node.longitude));
}

function buildNodeGeoJson(nodes, hoveredNodeId) {
    return {
        type: "FeatureCollection",
        features: nodes.map((node) => {
            const battery = resolveBatteryColor(node.batteryLevelPercent);
            const isHovered = node.nodeId === hoveredNodeId;
            return {
                type: "Feature",
                id: node.nodeId,
                geometry: {
                    type: "Point",
                    coordinates: [node.longitude, node.latitude]
                },
                properties: {
                    nodeId: node.nodeId,
                    displayName: node.displayName,
                    channel: node.channel ?? "",
                    fillColor: battery.fill,
                    strokeColor: isHovered ? resolveChannelColorHex(node.channel) : battery.stroke,
                    radius: isHovered ? 9 : 7,
                    strokeWidth: isHovered ? 3 : 2,
                    showLabel: isHovered ? 1 : 0
                }
            };
        })
    };
}

function buildLinkGeoJson(hoveredNode, peers) {
    if (!hoveredNode || peers.length === 0) {
        return { type: "FeatureCollection", features: [] };
    }

    return {
        type: "FeatureCollection",
        features: peers.map((peer) => ({
            type: "Feature",
            geometry: {
                type: "LineString",
                coordinates: [
                    [hoveredNode.longitude, hoveredNode.latitude],
                    [peer.longitude, peer.latitude]
                ]
            },
            properties: {
                color: resolveChannelColorHex(hoveredNode.channel),
                nodeId: hoveredNode.nodeId
            }
        }))
    };
}

function distanceBetweenNodes(source, target) {
    const latitudeDelta = toRadians(target.latitude - source.latitude);
    const longitudeDelta = toRadians(target.longitude - source.longitude);
    const sourceLatitude = toRadians(source.latitude);
    const targetLatitude = toRadians(target.latitude);

    const haversine =
        Math.sin(latitudeDelta / 2) * Math.sin(latitudeDelta / 2) +
        Math.cos(sourceLatitude) * Math.cos(targetLatitude) *
        Math.sin(longitudeDelta / 2) * Math.sin(longitudeDelta / 2);

    return 2 * 6371000 * Math.atan2(Math.sqrt(haversine), Math.sqrt(1 - haversine));
}

function toRadians(value) {
    return value * Math.PI / 180;
}

function findFirstLabelLayerId(map) {
    for (const layer of map.getStyle().layers) {
        if (layer.type === "symbol" && layer.layout?.["text-field"]) {
            return layer.id;
        }
    }

    return undefined;
}

function addBuildingsLayer(map, layerId) {
    const labelLayerId = findFirstLabelLayerId(map);

    map.addLayer(
        {
            id: layerId,
            type: "fill-extrusion",
            source: "openmaptiles",
            "source-layer": "building",
            minzoom: 15,
            filter: ["!=", ["get", "hide_3d"], true],
            paint: {
                "fill-extrusion-color": "#ddd8cf",
                "fill-extrusion-height": [
                    "interpolate", ["linear"], ["zoom"],
                    15, 0,
                    16, ["get", "render_height"]
                ],
                "fill-extrusion-base": [
                    "case",
                    [">=", ["get", "zoom"], 16],
                    ["get", "render_min_height"],
                    0
                ],
                "fill-extrusion-opacity": 0.82
            }
        },
        labelLayerId
    );
}

function buildContourSourceData() {
    if (!demSource) {
        return null;
    }

    return {
        type: "vector",
        tiles: [
            demSource.contourProtocolUrl({
                multiplier: 1,
                overzoom: 1,
                thresholds: {
                    11: [200, 1000],
                    12: [100, 500],
                    13: [50, 200],
                    14: [20, 100],
                    15: [10, 50]
                },
                elevationKey: "ele",
                levelKey: "level",
                contourLayer: "contours"
            })
        ],
        maxzoom: 15
    };
}

function addContourLayers(map, sourceId, layerId) {
    const labelLayerId = findFirstLabelLayerId(map);

    map.addLayer(
        {
            id: layerId,
            type: "line",
            source: sourceId,
            "source-layer": "contours",
            paint: {
                "line-color": "#b8a896",
                "line-opacity": ["match", ["get", "level"], 1, 0.6, 0.3],
                "line-width": ["match", ["get", "level"], 1, 1, 0.5]
            }
        },
        labelLayerId
    );
}

function createMap(container, center, zoom) {
    const map = new window.maplibregl.Map({
        container,
        style: STYLE_URL,
        center: center ?? DEFAULT_CENTER,
        zoom: zoom ?? DEFAULT_ZOOM,
        projection: "mercator",
        attributionControl: false
    });

    map.addControl(new window.maplibregl.AttributionControl({ compact: true }), "bottom-left");

    return map;
}

function addMapSources(map) {
    map.addSource(NODE_SOURCE_ID, {
        type: "geojson",
        data: { type: "FeatureCollection", features: [] }
    });

    map.addSource(LINK_SOURCE_ID, {
        type: "geojson",
        data: { type: "FeatureCollection", features: [] }
    });

    map.addSource(PULSE_SOURCE_ID, {
        type: "geojson",
        data: { type: "FeatureCollection", features: [] }
    });

    const contourSourceData = buildContourSourceData();
    if (contourSourceData) {
        map.addSource(CONTOUR_SOURCE_ID, contourSourceData);
    }
}

function addMapLayers(map) {
    addBuildingsLayer(map, BUILDINGS_LAYER_ID);

    if (map.getSource(CONTOUR_SOURCE_ID)) {
        addContourLayers(map, CONTOUR_SOURCE_ID, CONTOUR_LAYER_ID);
    }

    map.addLayer({
        id: LINK_LAYER_ID,
        type: "line",
        source: LINK_SOURCE_ID,
        paint: {
            "line-color": ["get", "color"],
            "line-width": 2.5,
            "line-opacity": 0.72
        }
    });

    map.addLayer({
        id: PULSE_LAYER_ID,
        type: "circle",
        source: PULSE_SOURCE_ID,
        paint: {
            "circle-radius": ["get", "radius"],
            "circle-color": ["get", "color"],
            "circle-opacity": ["get", "opacity"],
            "circle-stroke-width": 0
        }
    });

    map.addLayer({
        id: NODE_LAYER_ID,
        type: "circle",
        source: NODE_SOURCE_ID,
        paint: {
            "circle-radius": ["get", "radius"],
            "circle-color": ["get", "fillColor"],
            "circle-stroke-color": ["get", "strokeColor"],
            "circle-stroke-width": ["get", "strokeWidth"]
        }
    });

    map.addLayer({
        id: NODE_LABEL_LAYER_ID,
        type: "symbol",
        source: NODE_SOURCE_ID,
        filter: ["==", ["get", "showLabel"], 1],
        layout: {
            "text-field": ["concat", ["get", "displayName"], "\n", ["case",
                ["!=", ["get", "channel"], ""], ["concat", "Channel ", ["get", "channel"]],
                "Channel unknown"
            ]],
            "text-font": ["Noto Sans Bold", "Noto Sans Regular"],
            "text-size": 12,
            "text-offset": [0, -1.8],
            "text-anchor": "bottom",
            "text-max-width": 14
        },
        paint: {
            "text-color": "#f7efe1",
            "text-halo-color": "#10202f",
            "text-halo-width": 2
        }
    });
}

async function getOrCreateMapState(containerId, dotNetCallbackRef) {
    const existingState = mapStates.get(containerId);
    if (existingState) {
        existingState.dotNetCallbackRef = dotNetCallbackRef;
        return existingState;
    }

    const container = document.getElementById(containerId);
    if (!container) {
        return null;
    }

    const map = createMap(container);

    const mapState = {
        containerId,
        dotNetCallbackRef,
        map,
        nodeDataById: new Map(),
        hoveredNodeId: null,
        pinnedNodeId: null,
        didAutoFrame: false,
        activePulses: [],
        rafId: null
    };

    mapStates.set(containerId, mapState);

    await new Promise((resolve) => {
        map.once("load", () => {
            addMapSources(map);
            addMapLayers(map);
            wireInteractions(mapState);
            resolve();
        });
    });

    return mapState;
}

function wireInteractions(mapState) {
    const { map } = mapState;

    map.on("mousemove", NODE_LAYER_ID, (event) => {
        if (!event.features?.length) {
            return;
        }

        const nodeId = event.features[0].properties.nodeId;

        if (nodeId === mapState.hoveredNodeId) {
            return;
        }

        mapState.hoveredNodeId = nodeId;
        map.getCanvas().style.cursor = "pointer";
        refreshNodeAppearance(mapState);
        mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeHoveredFromMap", nodeId);
    });

    map.on("mouseleave", NODE_LAYER_ID, () => {
        if (mapState.pinnedNodeId) {
            mapState.hoveredNodeId = null;
            refreshNodeAppearance(mapState);
            mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeHoveredFromMap", null);
            return;
        }

        mapState.hoveredNodeId = null;
        map.getCanvas().style.cursor = "grab";
        refreshNodeAppearance(mapState);
        mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeHoveredFromMap", null);
    });

    map.on("click", NODE_LAYER_ID, (event) => {
        if (!event.features?.length) {
            return;
        }

        const nodeId = event.features[0].properties.nodeId;
        mapState.hoveredNodeId = nodeId;
        mapState.pinnedNodeId = mapState.pinnedNodeId === nodeId ? null : nodeId;
        refreshNodeAppearance(mapState);
        mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeSelectedFromMap", nodeId);
    });

    map.on("click", (event) => {
        const features = map.queryRenderedFeatures(event.point, { layers: [NODE_LAYER_ID] });
        if (features.length > 0) {
            return;
        }

        mapState.pinnedNodeId = null;
        mapState.hoveredNodeId = null;
        map.getCanvas().style.cursor = "grab";
        refreshNodeAppearance(mapState);
        mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeSelectedFromMap", null);
    });
}

function refreshNodeAppearance(mapState) {
    const activeNodeId = mapState.pinnedNodeId ?? mapState.hoveredNodeId;
    const nodes = Array.from(mapState.nodeDataById.values());

    mapState.map.getSource(NODE_SOURCE_ID)?.setData(buildNodeGeoJson(nodes, activeNodeId));

    const hoveredNode = activeNodeId ? mapState.nodeDataById.get(activeNodeId) : null;

    if (!hoveredNode) {
        mapState.map.getSource(LINK_SOURCE_ID)?.setData({ type: "FeatureCollection", features: [] });
        return;
    }

    const peers = nodes
        .filter((node) => node.nodeId !== hoveredNode.nodeId && node.channel && node.channel === hoveredNode.channel)
        .sort((a, b) => distanceBetweenNodes(hoveredNode, a) - distanceBetweenNodes(hoveredNode, b))
        .slice(0, HOVER_LINK_LIMIT);

    mapState.map.getSource(LINK_SOURCE_ID)?.setData(buildLinkGeoJson(hoveredNode, peers));
}

function maybeFrameCamera(mapState, nodes, fitCameraToNodes) {
    if (!fitCameraToNodes && mapState.didAutoFrame) {
        return;
    }

    mapState.didAutoFrame = true;

    if (nodes.length === 0) {
        mapState.map.flyTo({ center: DEFAULT_CENTER, zoom: DEFAULT_ZOOM, duration: 800 });
        return;
    }

    if (nodes.length === 1) {
        mapState.map.flyTo({
            center: [nodes[0].longitude, nodes[0].latitude],
            zoom: 10,
            duration: 800
        });
        return;
    }

    const lngs = nodes.map((n) => n.longitude);
    const lats = nodes.map((n) => n.latitude);
    const bounds = [
        [Math.min(...lngs), Math.min(...lats)],
        [Math.max(...lngs), Math.max(...lats)]
    ];

    mapState.map.fitBounds(bounds, { padding: 80, maxZoom: 14, duration: 950 });
}

function triggerActivityPulses(mapState, activityPulses) {
    for (const activityPulse of activityPulses ?? []) {
        const nodeId = activityPulse?.nodeId;
        if (!nodeId) {
            continue;
        }

        const node = mapState.nodeDataById.get(nodeId);
        if (!node) {
            continue;
        }

        triggerActivityPulse(mapState, node);
    }
}

function triggerActivityPulse(mapState, node) {
    const startedAt = performance.now();
    mapState.activePulses.push({
        node,
        startedAt,
        duration: 1200,
        maxRadius: 40,
        color: resolveChannelColorHex(node.channel)
    });
    schedulePulseFrame(mapState);
}

function schedulePulseFrame(mapState) {
    if (mapState.rafId !== null) {
        return;
    }

    mapState.rafId = requestAnimationFrame((now) => {
        mapState.rafId = null;
        updatePulses(mapState, now);
    });
}

function updatePulses(mapState, now) {
    if (!mapStates.has(mapState.containerId)) {
        return;
    }

    mapState.activePulses = mapState.activePulses.filter((pulse) => (now - pulse.startedAt) < pulse.duration);

    const features = mapState.activePulses.map((pulse) => {
        const progress = Math.min(1, (now - pulse.startedAt) / pulse.duration);
        return {
            type: "Feature",
            geometry: { type: "Point", coordinates: [pulse.node.longitude, pulse.node.latitude] },
            properties: {
                radius: progress * pulse.maxRadius,
                color: pulse.color,
                opacity: 0.7 * (1 - progress)
            }
        };
    });

    mapState.map.getSource(PULSE_SOURCE_ID)?.setData({ type: "FeatureCollection", features });

    if (mapState.activePulses.length > 0) {
        schedulePulseFrame(mapState);
    } else {
        mapState.map.getSource(PULSE_SOURCE_ID)?.setData({ type: "FeatureCollection", features: [] });
    }
}

// --- Mini map (node details modal) ---

export async function renderMiniMap(containerId, latitude, longitude) {
    await ensureMapLibre();

    const existing = miniMapStates.get(containerId);

    if (existing) {
        existing.map.setCenter([longitude, latitude]);
        existing.map.getSource(MINI_NODE_SOURCE_ID)?.setData({
            type: "FeatureCollection",
            features: [{
                type: "Feature",
                geometry: { type: "Point", coordinates: [longitude, latitude] },
                properties: {}
            }]
        });
        return;
    }

    const container = document.getElementById(containerId);
    if (!container) {
        return;
    }

    const map = createMap(container, [longitude, latitude], 14);
    miniMapStates.set(containerId, { containerId, map });

    await new Promise((resolve) => {
        map.once("load", () => {
            addBuildingsLayer(map, MINI_BUILDINGS_LAYER_ID);

            const contourSourceData = buildContourSourceData();
            if (contourSourceData) {
                map.addSource(MINI_CONTOUR_SOURCE_ID, contourSourceData);
                addContourLayers(map, MINI_CONTOUR_SOURCE_ID, MINI_CONTOUR_LAYER_ID);
            }

            map.addSource(MINI_NODE_SOURCE_ID, {
                type: "geojson",
                data: {
                    type: "FeatureCollection",
                    features: [{
                        type: "Feature",
                        geometry: { type: "Point", coordinates: [longitude, latitude] },
                        properties: {}
                    }]
                }
            });

            map.addLayer({
                id: MINI_NODE_LAYER_ID,
                type: "circle",
                source: MINI_NODE_SOURCE_ID,
                paint: {
                    "circle-radius": 8,
                    "circle-color": "#6bb6ef",
                    "circle-stroke-color": "#2f6ea8",
                    "circle-stroke-width": 2.5
                }
            });

            resolve();
        });
    });
}

export function disposeMiniMap(containerId) {
    const miniState = miniMapStates.get(containerId);
    if (!miniState) {
        return;
    }

    miniState.map.remove();
    miniMapStates.delete(containerId);
}

// --- Main node map ---

export async function renderNodeMap(containerId, nodes, activityPulses, fitCameraToNodes, dotNetCallbackRef) {
    await ensureMapLibre();

    const mapState = await getOrCreateMapState(containerId, dotNetCallbackRef);
    if (!mapState) {
        return;
    }

    const normalizedNodes = normalizeNodes(nodes);
    mapState.nodeDataById = new Map(normalizedNodes.map((n) => [n.nodeId, n]));

    if (mapState.hoveredNodeId && !mapState.nodeDataById.has(mapState.hoveredNodeId)) {
        mapState.hoveredNodeId = null;
    }

    if (mapState.pinnedNodeId && !mapState.nodeDataById.has(mapState.pinnedNodeId)) {
        mapState.pinnedNodeId = null;
    }

    refreshNodeAppearance(mapState);
    maybeFrameCamera(mapState, normalizedNodes, Boolean(fitCameraToNodes));
    triggerActivityPulses(mapState, activityPulses);
}

export function setPinnedNode(containerId, nodeId) {
    const mapState = mapStates.get(containerId);
    if (!mapState) {
        return;
    }

    mapState.pinnedNodeId = typeof nodeId === "string" && nodeId.length > 0 ? nodeId : null;
    refreshNodeAppearance(mapState);
}

export function disposeNodeMap(containerId) {
    const mapState = mapStates.get(containerId);
    if (!mapState) {
        return;
    }

    if (mapState.rafId !== null) {
        cancelAnimationFrame(mapState.rafId);
        mapState.rafId = null;
    }

    mapState.map.remove();
    mapStates.delete(containerId);
}
