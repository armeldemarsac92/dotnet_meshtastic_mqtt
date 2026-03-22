const MAPLIBRE_SCRIPT_ID = "meshboard-maplibre-script";
const MAPLIBRE_SCRIPT_URL = "https://unpkg.com/maplibre-gl@^5/dist/maplibre-gl.js";
const DECKGL_SCRIPT_ID = "meshboard-deckgl-script";
const DECKGL_SCRIPT_URL = "https://unpkg.com/deck.gl@^9.0.0/dist.min.js";
const STYLE_URL = "https://tiles.openfreemap.org/styles/positron";
const DEM_TILES_URL = "https://demotiles.maplibre.org/terrain-tiles/{z}/{x}/{y}.png";
const DEFAULT_CENTER = [0, 20];
const DEFAULT_ZOOM = 2;
const CONTOUR_SOURCE_ID = "meshboard-contours";
const BUILDINGS_LAYER_ID = "meshboard-buildings";
const CONTOUR_LAYER_ID = "meshboard-contour-lines";
const NODE_LAYER_ID = "meshboard-node-circles";
const NODE_LABEL_LAYER_ID = "meshboard-node-labels";
const RADIO_LINK_LAYER_ID = "meshboard-radio-link-lines";
const MINI_NODE_SOURCE_ID = "meshboard-mini-node";
const MINI_CONTOUR_SOURCE_ID = "meshboard-mini-contours";
const MINI_NODE_LAYER_ID = "meshboard-mini-node-circle";
const MINI_BUILDINGS_LAYER_ID = "meshboard-mini-buildings";
const MINI_CONTOUR_LAYER_ID = "meshboard-mini-contour-lines";

let maplibreLoadPromise = null;
let deckGlLoadPromise = null;
let demSource = null;
const colorCache = new Map();
const mapStates = new Map();
const miniMapStates = new Map();

function ensureExternalScript(scriptId, scriptUrl, readyCheck, errorMessage) {
    const existing = document.getElementById(scriptId);

    if (existing) {
        if (readyCheck()) {
            return Promise.resolve();
        }

        return new Promise((resolve, reject) => {
            existing.addEventListener("load", () => resolve(), { once: true });
            existing.addEventListener("error", () => reject(new Error(errorMessage)), { once: true });
        });
    }

    return new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.id = scriptId;
        script.src = scriptUrl;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error(errorMessage));
        document.head.appendChild(script);
    });
}

function ensureMapLibreScript() {
    return ensureExternalScript(
        MAPLIBRE_SCRIPT_ID,
        MAPLIBRE_SCRIPT_URL,
        () => Boolean(window.maplibregl),
        "Failed to load MapLibre GL JS.");
}

function ensureDeckGlScript() {
    return ensureExternalScript(
        DECKGL_SCRIPT_ID,
        DECKGL_SCRIPT_URL,
        () => Boolean(window.deck?.MapboxOverlay),
        "Failed to load deck.gl.");
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

async function ensureDeckGl() {
    if (window.deck?.MapboxOverlay) {
        return;
    }

    if (!deckGlLoadPromise) {
        deckGlLoadPromise = ensureDeckGlScript();
    }

    await deckGlLoadPromise;
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

function toRgba(hexColor, alpha = 255) {
    const normalizedAlpha = Math.max(0, Math.min(255, Math.round(alpha)));
    const cacheKey = `${hexColor}:${normalizedAlpha}`;
    const cached = colorCache.get(cacheKey);
    if (cached) {
        return cached;
    }

    const normalizedHex = hexColor.startsWith("#")
        ? hexColor.slice(1)
        : hexColor;

    const value = normalizedHex.length === 3
        ? normalizedHex.split("").map((part) => `${part}${part}`).join("")
        : normalizedHex;

    if (value.length !== 6) {
        return [160, 160, 160, normalizedAlpha];
    }

    const rgba = [
        Number.parseInt(value.slice(0, 2), 16),
        Number.parseInt(value.slice(2, 4), 16),
        Number.parseInt(value.slice(4, 6), 16),
        normalizedAlpha
    ];

    colorCache.set(cacheKey, rgba);
    return rgba;
}

function withAlpha(color, alpha) {
    if (!Array.isArray(color) || color.length < 3) {
        return [160, 160, 160, Math.max(0, Math.min(255, Math.round(alpha)))];
    }

    return [color[0], color[1], color[2], Math.max(0, Math.min(255, Math.round(alpha)))];
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

function buildRenderableNodes(nodes) {
    return nodes.map((node) => {
        const battery = resolveBatteryColor(node.batteryLevelPercent);
        const channelColorHex = resolveChannelColorHex(node.channel);

        return {
            ...node,
            position: [node.longitude, node.latitude],
            fillColor: toRgba(battery.fill, 232),
            strokeColor: toRgba(battery.stroke, 255),
            highlightStrokeColor: toRgba(channelColorHex, 255),
            pulseColor: toRgba(channelColorHex, 255)
        };
    });
}

function normalizeRadioLinks(radioLinks) {
    return (radioLinks ?? [])
        .map((link) => ({
            sourceNodeId: link.sourceNodeId,
            targetNodeId: link.targetNodeId,
            snrDb: Number.isFinite(Number.parseFloat(link.snrDb))
                ? Number.parseFloat(link.snrDb)
                : null,
            sourceLatitude: Number.parseFloat(link.sourceLatitude),
            sourceLongitude: Number.parseFloat(link.sourceLongitude),
            targetLatitude: Number.parseFloat(link.targetLatitude),
            targetLongitude: Number.parseFloat(link.targetLongitude)
        }))
        .filter((link) =>
            link.sourceNodeId &&
            link.targetNodeId &&
            Number.isFinite(link.sourceLatitude) &&
            Number.isFinite(link.sourceLongitude) &&
            Number.isFinite(link.targetLatitude) &&
            Number.isFinite(link.targetLongitude));
}

function buildRenderableRadioLinks(radioLinks) {
    return radioLinks.map((link) => ({
        ...link,
        sourcePosition: [link.sourceLongitude, link.sourceLatitude],
        targetPosition: [link.targetLongitude, link.targetLatitude],
        color: toRgba(resolveSnrColorHex(link.snrDb), Math.round(255 * 0.7)),
        width: resolveSnrWidth(link.snrDb)
    }));
}

function resolveSnrColorHex(snrDb) {
    if (!Number.isFinite(snrDb)) {
        return "#a0a0a0";
    }

    if (snrDb < -10) {
        return "#e34a33";
    }

    if (snrDb <= 0) {
        return "#fdbb84";
    }

    return "#31a354";
}

function resolveSnrWidth(snrDb) {
    if (!Number.isFinite(snrDb)) {
        return 1.5;
    }

    if (snrDb < -10) {
        return 1.25;
    }

    if (snrDb <= 0) {
        return 2;
    }

    return 3;
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
}

function addDeckOverlay(mapState) {
    const overlay = new window.deck.MapboxOverlay({
        interleaved: false,
        layers: [],
        pickingRadius: 8,
        useDevicePixels: Math.min(window.devicePixelRatio || 1, 2)
    });

    mapState.map.addControl(overlay);
    mapState.deckOverlay = overlay;

    const canvas = overlay.getCanvas?.();
    if (canvas) {
        canvas.style.pointerEvents = "none";
    }
}

function buildLineLayer(layerId, data, visible) {
    if (!visible || !Array.isArray(data) || data.length === 0) {
        return null;
    }

    return new window.deck.LineLayer({
        id: layerId,
        data,
        pickable: false,
        widthUnits: "pixels",
        widthMinPixels: 1,
        getSourcePosition: (line) => line.sourcePosition,
        getTargetPosition: (line) => line.targetPosition,
        getColor: (line) => line.color,
        getWidth: (line) => line.width,
        parameters: { depthTest: false }
    });
}

function buildNodeLayer(nodes, activeNodeId) {
    return new window.deck.ScatterplotLayer({
        id: NODE_LAYER_ID,
        data: nodes,
        pickable: true,
        stroked: true,
        filled: true,
        radiusUnits: "pixels",
        lineWidthUnits: "pixels",
        radiusMinPixels: 6,
        getPosition: (node) => node.position,
        getRadius: (node) => node.nodeId === activeNodeId ? 9 : 7,
        getFillColor: (node) => node.fillColor,
        getLineColor: (node) => node.nodeId === activeNodeId ? node.highlightStrokeColor : node.strokeColor,
        getLineWidth: (node) => node.nodeId === activeNodeId ? 3 : 2,
        parameters: { depthTest: false }
    });
}

function buildLabelLayer(activeNode) {
    if (!activeNode) {
        return null;
    }

    const label = activeNode.channel
        ? `${activeNode.displayName} · Channel ${activeNode.channel}`
        : `${activeNode.displayName} · Channel unknown`;

    return new window.deck.TextLayer({
        id: NODE_LABEL_LAYER_ID,
        data: [{ position: activeNode.position, label }],
        pickable: false,
        background: true,
        sizeUnits: "pixels",
        sizeMinPixels: 12,
        getPosition: (entry) => entry.position,
        getText: (entry) => entry.label,
        getSize: 12,
        getColor: [247, 239, 225, 255],
        getBackgroundColor: [16, 32, 47, 228],
        getBorderColor: [16, 32, 47, 255],
        getBorderWidth: 1,
        getPixelOffset: [0, -24],
        getTextAnchor: "middle",
        getAlignmentBaseline: "bottom",
        fontFamily: "Noto Sans, sans-serif",
        parameters: { depthTest: false }
    });
}

function buildStaticDeckLayers(mapState) {
    const activeNodeId = mapState.pinnedNodeId ?? mapState.hoveredNodeId;
    const activeNode = activeNodeId
        ? mapState.nodeDataById.get(activeNodeId) ?? null
        : null;

    return {
        backgroundLayers: [
            buildLineLayer(
                RADIO_LINK_LAYER_ID,
                mapState.radioLinks,
                mapState.showRadioLinks && mapState.radioLinks.length > 0)
        ].filter(Boolean),
        foregroundLayers: [
            buildNodeLayer(mapState.nodes, activeNodeId),
            buildLabelLayer(activeNode)
        ].filter(Boolean)
    };
}

function refreshNodeAppearance(mapState, rebuildStaticLayers = true) {
    if (!mapState.deckOverlay) {
        return;
    }

    if (rebuildStaticLayers || !mapState.staticBackgroundLayers || !mapState.staticForegroundLayers) {
        const layers = buildStaticDeckLayers(mapState);
        mapState.staticBackgroundLayers = layers.backgroundLayers;
        mapState.staticForegroundLayers = layers.foregroundLayers;
    }

    mapState.deckOverlay.setProps({
        layers: [
            ...(mapState.staticBackgroundLayers ?? []),
            ...(mapState.staticForegroundLayers ?? [])
        ]
    });
}

function setCanvasCursor(mapState, cursor) {
    mapState.map.getCanvas().style.cursor = cursor;

    const overlayCanvas = mapState.deckOverlay?.getCanvas?.();
    if (overlayCanvas) {
        overlayCanvas.style.cursor = cursor;
    }
}

function pickNodeAtPoint(mapState, point) {
    if (!mapState.deckOverlay) {
        return null;
    }

    const result = mapState.deckOverlay.pickObject({
        x: point.x,
        y: point.y,
        radius: 8,
        layerIds: [NODE_LAYER_ID]
    });

    return result?.object ?? null;
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
        deckOverlay: null,
        nodeDataById: new Map(),
        nodes: [],
        radioLinks: [],
        showRadioLinks: false,
        staticBackgroundLayers: null,
        staticForegroundLayers: null,
        hoveredNodeId: null,
        pinnedNodeId: null,
        didAutoFrame: false
    };

    mapStates.set(containerId, mapState);

    await new Promise((resolve) => {
        map.once("load", () => {
            addMapSources(map);
            addMapLayers(map);
            addDeckOverlay(mapState);
            setCanvasCursor(mapState, "grab");
            wireInteractions(mapState);
            resolve();
        });
    });

    return mapState;
}

function wireInteractions(mapState) {
    const { map } = mapState;

    map.on("mousemove", (event) => {
        const pickedNode = pickNodeAtPoint(mapState, event.point);
        if (!pickedNode?.nodeId) {
            if (mapState.hoveredNodeId !== null) {
                mapState.hoveredNodeId = null;
                refreshNodeAppearance(mapState);
                mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeHoveredFromMap", null);
            }

            setCanvasCursor(mapState, "grab");
            return;
        }

        if (pickedNode.nodeId === mapState.hoveredNodeId) {
            setCanvasCursor(mapState, "pointer");
            return;
        }

        mapState.hoveredNodeId = pickedNode.nodeId;
        setCanvasCursor(mapState, "pointer");
        refreshNodeAppearance(mapState);
        mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeHoveredFromMap", pickedNode.nodeId);
    });

    map.on("mouseleave", () => {
        if (mapState.hoveredNodeId === null) {
            setCanvasCursor(mapState, "grab");
            return;
        }

        mapState.hoveredNodeId = null;
        setCanvasCursor(mapState, "grab");
        refreshNodeAppearance(mapState);
        mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeHoveredFromMap", null);
    });

    map.on("click", (event) => {
        const pickedNode = pickNodeAtPoint(mapState, event.point);
        if (pickedNode?.nodeId) {
            mapState.hoveredNodeId = pickedNode.nodeId;
            mapState.pinnedNodeId = mapState.pinnedNodeId === pickedNode.nodeId
                ? null
                : pickedNode.nodeId;

            setCanvasCursor(mapState, "pointer");
            refreshNodeAppearance(mapState);
            mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeSelectedFromMap", pickedNode.nodeId);
            return;
        }

        mapState.pinnedNodeId = null;
        mapState.hoveredNodeId = null;
        setCanvasCursor(mapState, "grab");
        refreshNodeAppearance(mapState);
        mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeSelectedFromMap", null);
    });
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

export async function renderNodeMap(
    containerId,
    nodes,
    fitCameraToNodes,
    dotNetCallbackRef,
    radioLinks = [],
    showRadioLinks = false) {
    await Promise.all([ensureMapLibre(), ensureDeckGl()]);

    const mapState = await getOrCreateMapState(containerId, dotNetCallbackRef);
    if (!mapState) {
        return;
    }

    const renderableNodes = buildRenderableNodes(normalizeNodes(nodes));
    mapState.nodes = renderableNodes;
    mapState.nodeDataById = new Map(renderableNodes.map((node) => [node.nodeId, node]));
    mapState.radioLinks = buildRenderableRadioLinks(normalizeRadioLinks(radioLinks));
    mapState.showRadioLinks = Boolean(showRadioLinks);

    if (mapState.hoveredNodeId && !mapState.nodeDataById.has(mapState.hoveredNodeId)) {
        mapState.hoveredNodeId = null;
    }

    if (mapState.pinnedNodeId && !mapState.nodeDataById.has(mapState.pinnedNodeId)) {
        mapState.pinnedNodeId = null;
    }

    refreshNodeAppearance(mapState);
    maybeFrameCamera(mapState, renderableNodes, Boolean(fitCameraToNodes));
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

    if (mapState.deckOverlay) {
        mapState.map.removeControl(mapState.deckOverlay);
        mapState.deckOverlay = null;
    }

    mapState.map.remove();
    mapStates.delete(containerId);
}
