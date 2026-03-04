const LEAFLET_CSS_ID = "meshboard-leaflet-css";
const LEAFLET_SCRIPT_ID = "meshboard-leaflet-script";
const LEAFLET_CSS_URL = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.css";
const LEAFLET_SCRIPT_URL = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.js";
const DEFAULT_CENTER = [20, 0];
const DEFAULT_ZOOM = 2;

let leafletLoadPromise = null;
const mapStates = new Map();

function ensureLeafletCss() {
    if (document.getElementById(LEAFLET_CSS_ID)) {
        return;
    }

    const link = document.createElement("link");
    link.id = LEAFLET_CSS_ID;
    link.rel = "stylesheet";
    link.href = LEAFLET_CSS_URL;
    link.crossOrigin = "";
    document.head.appendChild(link);
}

function ensureLeafletScript() {
    const existing = document.getElementById(LEAFLET_SCRIPT_ID);

    if (existing) {
        if (window.L) {
            return Promise.resolve();
        }

        return new Promise((resolve, reject) => {
            existing.addEventListener("load", () => resolve(), { once: true });
            existing.addEventListener("error", () => reject(new Error("Failed to load Leaflet script.")), { once: true });
        });
    }

    return new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.id = LEAFLET_SCRIPT_ID;
        script.src = LEAFLET_SCRIPT_URL;
        script.crossOrigin = "";
        script.onload = () => resolve();
        script.onerror = () => reject(new Error("Failed to load Leaflet script."));
        document.head.appendChild(script);
    });
}

async function ensureLeaflet() {
    if (window.L) {
        return;
    }

    if (!leafletLoadPromise) {
        ensureLeafletCss();
        leafletLoadPromise = ensureLeafletScript();
    }

    await leafletLoadPromise;
}

function resolveMarkerColor(batteryLevelPercent) {
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

function getOrCreateMapState(containerId) {
    const existingState = mapStates.get(containerId);

    if (existingState) {
        return existingState;
    }

    const container = document.getElementById(containerId);
    if (!container) {
        return null;
    }

    const map = window.L.map(container, {
        attributionControl: true,
        worldCopyJump: true
    });

    window.L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: "&copy; OpenStreetMap contributors"
    }).addTo(map);

    const markerLayer = window.L.layerGroup().addTo(map);
    map.setView(DEFAULT_CENTER, DEFAULT_ZOOM);

    const mapState = { map, markerLayer };
    mapStates.set(containerId, mapState);
    return mapState;
}

export async function renderNodeMap(containerId, nodes, dotNetCallbackRef) {
    await ensureLeaflet();

    const mapState = getOrCreateMapState(containerId);
    if (!mapState) {
        return;
    }

    mapState.markerLayer.clearLayers();

    const markerBounds = [];

    for (const node of nodes ?? []) {
        const latitude = Number.parseFloat(node.latitude);
        const longitude = Number.parseFloat(node.longitude);

        if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) {
            continue;
        }

        const color = resolveMarkerColor(Number.parseFloat(node.batteryLevelPercent));
        const marker = window.L.circleMarker([latitude, longitude], {
            radius: 7,
            weight: 2,
            color: color.stroke,
            fillColor: color.fill,
            fillOpacity: 0.9
        });

        marker.bindTooltip(node.displayName ?? node.nodeId ?? "Unknown", {
            direction: "top",
            offset: [0, -6],
            opacity: 0.9
        });

        marker.on("click", () => {
            marker.closeTooltip();
            dotNetCallbackRef.invokeMethodAsync("OnNodeSelectedFromMap", node.nodeId);
        });

        marker.addTo(mapState.markerLayer);
        markerBounds.push([latitude, longitude]);
    }

    if (markerBounds.length === 0) {
        mapState.map.setView(DEFAULT_CENTER, DEFAULT_ZOOM);
    } else if (markerBounds.length === 1) {
        mapState.map.setView(markerBounds[0], 12);
    } else {
        mapState.map.fitBounds(window.L.latLngBounds(markerBounds), { padding: [26, 26] });
    }

    requestAnimationFrame(() => mapState.map.invalidateSize());
}

export function disposeNodeMap(containerId) {
    const mapState = mapStates.get(containerId);
    if (!mapState) {
        return;
    }

    mapState.map.remove();
    mapStates.delete(containerId);
}
