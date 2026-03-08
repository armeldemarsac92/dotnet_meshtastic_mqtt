const CESIUM_VERSION = "1.138";
const CESIUM_BASE_URL = `https://cesium.com/downloads/cesiumjs/releases/${CESIUM_VERSION}/Build/Cesium`;
const CESIUM_CSS_ID = "meshboard-cesium-css";
const CESIUM_SCRIPT_ID = "meshboard-cesium-script";
const CESIUM_CSS_URL = `${CESIUM_BASE_URL}/Widgets/widgets.css`;
const CESIUM_SCRIPT_URL = `${CESIUM_BASE_URL}/Cesium.js`;
const DEFAULT_CAMERA = {
    latitude: 20,
    longitude: 0,
    height: 22000000
};
const HOVER_LINK_LIMIT = 18;
const HOVER_CLEAR_DELAY_MS = 180;
const MAX_RENDER_RESOLUTION_SCALE = 2;
const NODE_ENTITY_PREFIX = "node:";
const LINK_ALTITUDE_METERS = 120;
const LIGHT_BASEMAP_URL = "https://services.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Light_Gray_Base/MapServer";

let cesiumLoadPromise = null;
const mapStates = new Map();

function ensureCesiumCss() {
    if (document.getElementById(CESIUM_CSS_ID)) {
        return;
    }

    const link = document.createElement("link");
    link.id = CESIUM_CSS_ID;
    link.rel = "stylesheet";
    link.href = CESIUM_CSS_URL;
    document.head.appendChild(link);
}

function ensureCesiumScript() {
    const existing = document.getElementById(CESIUM_SCRIPT_ID);

    if (existing) {
        if (window.Cesium) {
            return Promise.resolve();
        }

        return new Promise((resolve, reject) => {
            existing.addEventListener("load", () => resolve(), { once: true });
            existing.addEventListener("error", () => reject(new Error("Failed to load CesiumJS.")), { once: true });
        });
    }

    return new Promise((resolve, reject) => {
        window.CESIUM_BASE_URL = CESIUM_BASE_URL;

        const script = document.createElement("script");
        script.id = CESIUM_SCRIPT_ID;
        script.src = CESIUM_SCRIPT_URL;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error("Failed to load CesiumJS."));
        document.head.appendChild(script);
    });
}

async function ensureCesium() {
    if (window.Cesium) {
        return;
    }

    if (!cesiumLoadPromise) {
        ensureCesiumCss();
        cesiumLoadPromise = ensureCesiumScript();
    }

    await cesiumLoadPromise;
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

async function createBaseImageryProvider(Cesium) {
    try {
        return await Cesium.ArcGisMapServerImageryProvider.fromUrl(LIGHT_BASEMAP_URL);
    }
    catch {
        return new Cesium.OpenStreetMapImageryProvider({
            maximumLevel: 19,
            url: "https://tile.openstreetmap.org/"
        });
    }
}

async function createViewer(container) {
    const Cesium = window.Cesium;
    const viewer = new Cesium.Viewer(container, {
        animation: false,
        baseLayerPicker: false,
        fullscreenButton: false,
        geocoder: false,
        homeButton: false,
        infoBox: false,
        navigationHelpButton: false,
        projectionPicker: false,
        sceneModePicker: false,
        selectionIndicator: false,
        timeline: false,
        shouldAnimate: true
    });

    viewer.imageryLayers.removeAll();
    viewer.imageryLayers.addImageryProvider(await createBaseImageryProvider(Cesium));

    const baseLayer = viewer.imageryLayers.get(0);
    if (baseLayer) {
        baseLayer.alpha = 0.92;
        baseLayer.brightness = 1.12;
        baseLayer.contrast = 0.76;
        baseLayer.gamma = 1.02;
        baseLayer.saturation = 0.05;
    }

    viewer.resolutionScale = Math.min(window.devicePixelRatio || 1, MAX_RENDER_RESOLUTION_SCALE);
    viewer.scene.postProcessStages.fxaa.enabled = true;
    viewer.scene.backgroundColor = Cesium.Color.fromCssColorString("#f7f9fc");
    viewer.scene.globe.baseColor = Cesium.Color.fromCssColorString("#f2f5f9");
    viewer.scene.globe.enableLighting = false;
    viewer.scene.globe.showGroundAtmosphere = false;
    viewer.scene.skyAtmosphere.show = false;
    viewer.scene.fog.enabled = false;
    viewer.scene.requestRenderMode = true;
    viewer.scene.maximumRenderTimeChange = Number.POSITIVE_INFINITY;
    viewer.scene.screenSpaceCameraController.minimumZoomDistance = 25000;
    viewer.scene.screenSpaceCameraController.maximumZoomDistance = 50000000;

    if (viewer.cesiumWidget?.screenSpaceEventHandler) {
        viewer.cesiumWidget.screenSpaceEventHandler.removeInputAction(Cesium.ScreenSpaceEventType.LEFT_DOUBLE_CLICK);
    }

    viewer.camera.setView({
        destination: Cesium.Cartesian3.fromDegrees(
            DEFAULT_CAMERA.longitude,
            DEFAULT_CAMERA.latitude,
            DEFAULT_CAMERA.height)
    });

    return viewer;
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

    const viewer = await createViewer(container);
    const mapState = {
        containerId,
        dotNetCallbackRef,
        viewer,
        interactionHandler: null,
        hoverClearTimeoutId: null,
        nodeDataById: new Map(),
        nodeEntities: new Map(),
        linkEntities: [],
        hoveredNodeId: null,
        pinnedNodeId: null,
        didAutoFrame: false
    };

    mapStates.set(containerId, mapState);
    wireInteractions(mapState);
    return mapState;
}

function wireInteractions(mapState) {
    const Cesium = window.Cesium;
    const handler = new Cesium.ScreenSpaceEventHandler(mapState.viewer.scene.canvas);

    handler.setInputAction((movement) => {
        const nodeId = resolvePickedNodeIdAtPosition(mapState.viewer.scene, movement.endPosition);

        if (nodeId) {
            clearPendingHoverClear(mapState);
        }

        if (nodeId === mapState.hoveredNodeId) {
            return;
        }

        if (!nodeId) {
            scheduleHoverClear(mapState);
            return;
        }

        mapState.hoveredNodeId = nodeId;
        syncHoverState(mapState);
        mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeHoveredFromMap", nodeId);
    }, Cesium.ScreenSpaceEventType.MOUSE_MOVE);

    handler.setInputAction((movement) => {
        const nodeId = resolvePickedNodeIdAtPosition(mapState.viewer.scene, movement.position);

        mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeSelectedFromMap", nodeId);

        if (!nodeId) {
            mapState.pinnedNodeId = null;
            mapState.hoveredNodeId = null;
            syncHoverState(mapState);
            return;
        }

        clearPendingHoverClear(mapState);
        mapState.hoveredNodeId = nodeId;
        mapState.pinnedNodeId = mapState.pinnedNodeId === nodeId
            ? null
            : nodeId;
        syncHoverState(mapState);
    }, Cesium.ScreenSpaceEventType.LEFT_CLICK);

    mapState.interactionHandler = handler;
}

function getPickResults(scene, position) {
    if (!position) {
        return [];
    }

    try {
        return scene.drillPick(position, 10) ?? [];
    }
    catch {
        const pickedObject = scene.pick(position);
        return pickedObject ? [pickedObject] : [];
    }
}

function resolvePickedNodeIdAtPosition(scene, position) {
    for (const pickedObject of getPickResults(scene, position)) {
        const nodeId = resolvePickedNodeId(pickedObject);
        if (nodeId) {
            return nodeId;
        }
    }

    return null;
}

function resolvePickedNodeId(pickedObject) {
    if (!pickedObject?.id) {
        return null;
    }

    const entity = pickedObject.id;
    const rawId = typeof entity === "string" ? entity : entity.id;

    if (typeof rawId !== "string" || !rawId.startsWith(NODE_ENTITY_PREFIX)) {
        return typeof entity?.meshboardHoverNodeId === "string"
            ? entity.meshboardHoverNodeId
            : null;
    }

    return rawId.slice(NODE_ENTITY_PREFIX.length);
}

function clearPendingHoverClear(mapState) {
    if (!mapState.hoverClearTimeoutId) {
        return;
    }

    window.clearTimeout(mapState.hoverClearTimeoutId);
    mapState.hoverClearTimeoutId = null;
}

function scheduleHoverClear(mapState) {
    if (mapState.pinnedNodeId) {
        mapState.hoveredNodeId = null;
        mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeHoveredFromMap", null);
        return;
    }

    clearPendingHoverClear(mapState);
    mapState.hoverClearTimeoutId = window.setTimeout(() => {
        mapState.hoverClearTimeoutId = null;

        if (!mapStates.has(mapState.containerId) || mapState.viewer.isDestroyed()) {
            return;
        }

        if (mapState.hoveredNodeId === null) {
            return;
        }

        mapState.hoveredNodeId = null;
        syncHoverState(mapState);
        mapState.dotNetCallbackRef?.invokeMethodAsync("OnNodeHoveredFromMap", null);
    }, HOVER_CLEAR_DELAY_MS);
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

function syncNodeEntities(mapState, nodes) {
    const Cesium = window.Cesium;
    const nextNodeIds = new Set();

    for (const node of nodes) {
        nextNodeIds.add(node.nodeId);
        mapState.nodeDataById.set(node.nodeId, node);

        let entity = mapState.nodeEntities.get(node.nodeId);
        const isHovered = node.nodeId === mapState.hoveredNodeId;

        if (!entity) {
            entity = mapState.viewer.entities.add({
                id: `${NODE_ENTITY_PREFIX}${node.nodeId}`,
                position: Cesium.Cartesian3.fromDegrees(node.longitude, node.latitude, 0),
                point: {
                    pixelSize: 12,
                    color: Cesium.Color.WHITE,
                    outlineColor: Cesium.Color.BLACK,
                    outlineWidth: 2,
                    heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
                    disableDepthTestDistance: Number.POSITIVE_INFINITY
                },
                label: {
                    text: node.displayName,
                    show: false,
                    font: "600 14px ui-sans-serif, system-ui, sans-serif",
                    fillColor: Cesium.Color.fromCssColorString("#f7efe1"),
                    outlineColor: Cesium.Color.fromCssColorString("#132130"),
                    outlineWidth: 4,
                    style: Cesium.LabelStyle.FILL_AND_OUTLINE,
                    showBackground: true,
                    backgroundColor: Cesium.Color.fromCssColorString("#10202f").withAlpha(0.82),
                    pixelOffset: new Cesium.Cartesian2(0, -22),
                    verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
                    heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
                    disableDepthTestDistance: Number.POSITIVE_INFINITY
                }
            });

            mapState.nodeEntities.set(node.nodeId, entity);
        }

        entity.position = Cesium.Cartesian3.fromDegrees(node.longitude, node.latitude, 0);
        entity.label.text = buildNodeLabel(node);
        applyNodeAppearance(entity, node, isHovered);
    }

    for (const [nodeId, entity] of mapState.nodeEntities.entries()) {
        if (nextNodeIds.has(nodeId)) {
            continue;
        }

        mapState.viewer.entities.remove(entity);
        mapState.nodeEntities.delete(nodeId);
        mapState.nodeDataById.delete(nodeId);
    }

    if (mapState.hoveredNodeId && !mapState.nodeDataById.has(mapState.hoveredNodeId)) {
        mapState.hoveredNodeId = null;
    }

    if (mapState.pinnedNodeId && !mapState.nodeDataById.has(mapState.pinnedNodeId)) {
        mapState.pinnedNodeId = null;
    }
}

function buildNodeLabel(node) {
    const channelLabel = node.channel ? `Channel ${node.channel}` : "Channel unknown";
    return `${node.displayName}\n${channelLabel}`;
}

function applyNodeAppearance(entity, node, isHovered) {
    const Cesium = window.Cesium;
    const batteryColor = resolveBatteryColor(node.batteryLevelPercent);
    const linkColor = resolveChannelColor(node.channel, 1);

    entity.point.pixelSize = isHovered ? 16 : 12;
    entity.point.color = Cesium.Color.fromCssColorString(batteryColor.fill);
    entity.point.outlineWidth = isHovered ? 3 : 2;
    entity.point.outlineColor = isHovered
        ? linkColor
        : Cesium.Color.fromCssColorString(batteryColor.stroke);
    entity.label.show = isHovered;
}

function syncHoverState(mapState) {
    clearHoverLinks(mapState);
    clearPendingHoverClear(mapState);

    const activeNodeId = mapState.pinnedNodeId ?? mapState.hoveredNodeId;

    for (const [nodeId, entity] of mapState.nodeEntities.entries()) {
        const node = mapState.nodeDataById.get(nodeId);
        if (!node) {
            continue;
        }

        applyNodeAppearance(entity, node, nodeId === activeNodeId);
    }

    const hoveredNode = activeNodeId
        ? mapState.nodeDataById.get(activeNodeId)
        : null;

    mapState.viewer.container.style.cursor = hoveredNode ? "pointer" : "grab";

    if (!hoveredNode?.channel) {
        mapState.viewer.scene.requestRender();
        return;
    }

    const peers = Array.from(mapState.nodeDataById.values())
        .filter((node) => node.nodeId !== hoveredNode.nodeId && node.channel === hoveredNode.channel)
        .sort((left, right) => distanceBetweenNodes(hoveredNode, left) - distanceBetweenNodes(hoveredNode, right))
        .slice(0, HOVER_LINK_LIMIT);

    if (peers.length === 0) {
        mapState.viewer.scene.requestRender();
        return;
    }

    const Cesium = window.Cesium;
    const linkColor = resolveChannelColor(hoveredNode.channel, 0.92);

    for (const peer of peers) {
        const linkMaterial = new Cesium.PolylineOutlineMaterialProperty({
            color: linkColor,
            outlineColor: Cesium.Color.fromCssColorString("#10202f").withAlpha(0.52),
            outlineWidth: 2
        });

        const linkEntity = mapState.viewer.entities.add({
            polyline: {
                positions: [
                    Cesium.Cartesian3.fromDegrees(hoveredNode.longitude, hoveredNode.latitude, LINK_ALTITUDE_METERS),
                    Cesium.Cartesian3.fromDegrees(peer.longitude, peer.latitude, LINK_ALTITUDE_METERS)
                ],
                width: 7,
                arcType: Cesium.ArcType.GEODESIC,
                material: linkMaterial,
                depthFailMaterial: linkMaterial,
                clampToGround: false
            }
        });

        linkEntity.meshboardHoverNodeId = hoveredNode.nodeId;
        mapState.linkEntities.push(linkEntity);
    }

    mapState.viewer.scene.requestRender();
}

function clearHoverLinks(mapState) {
    if (mapState.linkEntities.length === 0) {
        return;
    }

    for (const entity of mapState.linkEntities) {
        mapState.viewer.entities.remove(entity);
    }

    mapState.linkEntities = [];
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

function resolveChannelColor(channel, alpha) {
    const Cesium = window.Cesium;

    if (!channel) {
        return Cesium.Color.fromCssColorString("#6bb6ef").withAlpha(alpha);
    }

    let hash = 0;
    for (let index = 0; index < channel.length; index += 1) {
        hash = ((hash << 5) - hash) + channel.charCodeAt(index);
        hash |= 0;
    }

    const hue = (Math.abs(hash) % 360) / 360;
    return Cesium.Color.fromHsl(hue, 0.72, 0.58, alpha);
}

function maybeFrameCamera(mapState, nodes, fitCameraToNodes) {
    if (!fitCameraToNodes && mapState.didAutoFrame) {
        return;
    }

    const Cesium = window.Cesium;

    if (nodes.length === 0) {
        mapState.viewer.camera.flyTo({
            destination: Cesium.Cartesian3.fromDegrees(
                DEFAULT_CAMERA.longitude,
                DEFAULT_CAMERA.latitude,
                DEFAULT_CAMERA.height),
            duration: 0.8
        });
        mapState.didAutoFrame = true;
        return;
    }

    if (nodes.length === 1) {
        const node = nodes[0];
        mapState.viewer.camera.flyTo({
            destination: Cesium.Cartesian3.fromDegrees(node.longitude, node.latitude, 1800000),
            duration: 0.8
        });
        mapState.didAutoFrame = true;
        return;
    }

    const points = nodes.map((node) => Cesium.Cartesian3.fromDegrees(node.longitude, node.latitude, 0));
    const boundingSphere = Cesium.BoundingSphere.fromPoints(points);

    mapState.viewer.camera.flyToBoundingSphere(boundingSphere, {
        duration: 0.95,
        offset: new Cesium.HeadingPitchRange(0, -1.15, Math.max(boundingSphere.radius * 3.2, 2200000))
    });

    mapState.didAutoFrame = true;
}

function triggerActivityPulses(mapState, activityPulses) {
    for (const activityPulse of activityPulses ?? []) {
        const nodeId = activityPulse?.nodeId;
        const pulseCount = Number.isFinite(Number(activityPulse?.pulseCount))
            ? Math.max(1, Number(activityPulse.pulseCount))
            : 1;

        if (!nodeId) {
            continue;
        }

        for (let pulseIndex = 0; pulseIndex < pulseCount; pulseIndex += 1) {
            window.setTimeout(() => {
                const currentState = mapStates.get(mapState.containerId);
                if (!currentState || currentState.viewer.isDestroyed()) {
                    return;
                }

                triggerActivityPulse(currentState, nodeId);
            }, pulseIndex * 180);
        }
    }
}

function triggerActivityPulse(mapState, nodeId) {
    const node = mapState.nodeDataById.get(nodeId);
    if (!node) {
        return;
    }

    const Cesium = window.Cesium;
    const pulseState = {
        radius: 2200,
        pointSize: 20,
        alpha: 0.95
    };

    const pulseColor = resolveChannelColor(node.channel, 0.98);
    const groundPulseEntity = mapState.viewer.entities.add({
        position: Cesium.Cartesian3.fromDegrees(node.longitude, node.latitude, 0),
        ellipse: {
            semiMajorAxis: new Cesium.CallbackProperty(() => pulseState.radius, false),
            semiMinorAxis: new Cesium.CallbackProperty(() => pulseState.radius, false),
            height: 0,
            heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
            material: new Cesium.ColorMaterialProperty(
                new Cesium.CallbackProperty(
                    () => pulseColor.withAlpha(Math.max(pulseState.alpha * 0.34, 0)),
                    false)),
            outline: true,
            outlineColor: new Cesium.CallbackProperty(
                () => pulseColor.withAlpha(Math.max(pulseState.alpha, 0)),
                false),
            outlineWidth: 4,
            zIndex: 20
        }
    });

    const pointPulseEntity = mapState.viewer.entities.add({
        position: Cesium.Cartesian3.fromDegrees(node.longitude, node.latitude, 0),
        point: {
            pixelSize: new Cesium.CallbackProperty(() => pulseState.pointSize, false),
            color: new Cesium.CallbackProperty(
                () => pulseColor.withAlpha(Math.max(pulseState.alpha * 0.9, 0)),
                false),
            outlineColor: new Cesium.CallbackProperty(
                () => Cesium.Color.WHITE.withAlpha(Math.max(pulseState.alpha * 0.95, 0)),
                false),
            outlineWidth: 3,
            heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
            disableDepthTestDistance: Number.POSITIVE_INFINITY
        }
    });

    const startedAt = performance.now();
    const durationMilliseconds = 1700;

    const animatePulse = (now) => {
        const currentState = mapStates.get(mapState.containerId);
        if (!currentState || currentState.viewer.isDestroyed()) {
            return;
        }

        const progress = Math.min(1, (now - startedAt) / durationMilliseconds);
        pulseState.radius = 2200 + (progress * 30000);
        pulseState.pointSize = 20 + (progress * 34);
        pulseState.alpha = 0.95 * (1 - progress);
        currentState.viewer.scene.requestRender();

        if (progress < 1) {
            requestAnimationFrame(animatePulse);
            return;
        }

        currentState.viewer.entities.remove(groundPulseEntity);
        currentState.viewer.entities.remove(pointPulseEntity);
        currentState.viewer.scene.requestRender();
    };

    requestAnimationFrame(animatePulse);
}

export async function renderNodeMap(containerId, nodes, activityPulses, fitCameraToNodes, dotNetCallbackRef) {
    await ensureCesium();

    const mapState = await getOrCreateMapState(containerId, dotNetCallbackRef);
    if (!mapState) {
        return;
    }

    const normalizedNodes = normalizeNodes(nodes);
    syncNodeEntities(mapState, normalizedNodes);
    syncHoverState(mapState);
    maybeFrameCamera(mapState, normalizedNodes, Boolean(fitCameraToNodes));
    triggerActivityPulses(mapState, activityPulses);
    mapState.viewer.scene.requestRender();
}

export function setPinnedNode(containerId, nodeId) {
    const mapState = mapStates.get(containerId);
    if (!mapState || mapState.viewer.isDestroyed()) {
        return;
    }

    mapState.pinnedNodeId = typeof nodeId === "string" && nodeId.length > 0
        ? nodeId
        : null;

    syncHoverState(mapState);
    mapState.viewer.scene.requestRender();
}

export function disposeNodeMap(containerId) {
    const mapState = mapStates.get(containerId);
    if (!mapState) {
        return;
    }

    clearHoverLinks(mapState);
    clearPendingHoverClear(mapState);
    mapState.interactionHandler?.destroy();

    if (!mapState.viewer.isDestroyed()) {
        mapState.viewer.destroy();
    }

    mapStates.delete(containerId);
}
