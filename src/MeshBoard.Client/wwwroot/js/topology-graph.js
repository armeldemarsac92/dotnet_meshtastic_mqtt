const GRAPHOLOGY_URL = "https://cdn.jsdelivr.net/npm/graphology@0.25.4/dist/graphology.umd.js";
const GRAPHOLOGY_LIB_URL = "https://cdn.jsdelivr.net/npm/graphology-library@0.8.0/dist/graphology-library.min.js";
const SIGMA_URL = "https://cdn.jsdelivr.net/npm/sigma@2.4.0/build/sigma.min.js";

// Design-system colours — distinct, accessible, warm/cool mix.
// Deliberately avoid #dc2626 (reserved for bridge nodes).
const COMMUNITY_PALETTE = [
    "#2563eb", "#7c3aed", "#059669", "#d97706",
    "#0891b2", "#65a30d", "#ea580c", "#4f46e5",
    "#db2777", "#0d9488", "#a16207", "#6366f1"
];

let scriptsLoadPromise = null;
const topologyInstances = new Map();

function loadScript(id, url, checkGlobal) {
    return new Promise((resolve, reject) => {
        if (checkGlobal()) return resolve();
        let script = document.getElementById(id);
        if (script) {
            script.addEventListener("load", resolve, { once: true });
            script.addEventListener("error", reject, { once: true });
            return;
        }
        script = document.createElement("script");
        script.id = id;
        script.src = url;
        script.onload = resolve;
        script.onerror = () => reject(new Error(`Failed to load ${url}`));
        document.head.appendChild(script);
    });
}

function ensureScripts() {
    if (scriptsLoadPromise) return scriptsLoadPromise;
    scriptsLoadPromise = Promise.all([
        loadScript("graphology-script", GRAPHOLOGY_URL, () => window.graphology),
        loadScript("graphology-lib-script", GRAPHOLOGY_LIB_URL, () => window.graphologyLibrary),
        loadScript("sigma-script", SIGMA_URL, () => window.Sigma)
    ]).catch(err => {
        scriptsLoadPromise = null;
        throw err;
    });
    return scriptsLoadPromise;
}

function snrHex(snrDb) {
    if (snrDb == null) return "#94A3B8AA";
    if (snrDb > 0)     return "#16A34AAA";
    if (snrDb >= -10)  return "#D97706AA";
    return "#DC2626AA";
}

/**
 * Three-phase organic layout:
 *   1. FA2 on within-community edges only → each cluster forms organic internal structure
 *   2. Community centroid explosion → clusters pushed far apart preserving internal shape
 *   3. Cross-community edges restored for display (bridge lines spanning between islands)
 *
 * Why centroid explosion instead of Noverlap:
 *   Noverlap treats each node independently and converges to a circular blob.
 *   Centroid explosion treats each CLUSTER as a unit — scales inter-centroid distances
 *   by a large factor so clusters become clearly separated islands with whitespace,
 *   while each cluster's internal organic structure is preserved intact.
 */
function runLayout(graph, settings) {
    const lib = window.graphologyLibrary;

    // 1. Community detection
    try { lib.communitiesLouvain.assign(graph); }
    catch (e) { console.warn("[topology-graph] Louvain failed:", e); }

    // 2. Seed: place each community's nodes in a random cluster position so they start
    //    spatially separated. Without this, FA2 convergence with no gravity leaves all
    //    communities overlapping at arbitrary positions.
    const communitySizes = new Map();
    graph.forEachNode((_, a) => {
        const c = a.community ?? 0;
        communitySizes.set(c, (communitySizes.get(c) ?? 0) + 1);
    });
    const communityList = [...communitySizes.keys()];
    const nComm = communityList.length;
    // Place each community centroid on a uniform random position in a wide field
    // Wide 2:1 seed area to force a non-circular overall shape
    const fieldW = Math.sqrt(nComm) * 140;
    const fieldH = Math.sqrt(nComm) * 70;
    const communitySeeds = new Map();
    communityList.forEach(c => {
        communitySeeds.set(c, {
            x: (Math.random() - 0.5) * 2 * fieldW,
            y: (Math.random() - 0.5) * 2 * fieldH,
        });
    });
    // Scatter each node near its community seed position
    graph.updateEachNodeAttributes((_node, attr) => {
        const c = attr.community ?? 0;
        const seed = communitySeeds.get(c) ?? { x: 0, y: 0 };
        const localR = Math.max(5, Math.sqrt(communitySizes.get(c) ?? 1) * 4);
        const angle = Math.random() * 2 * Math.PI;
        const r = Math.random() * localR;
        return { ...attr, x: seed.x + Math.cos(angle) * r, y: seed.y + Math.sin(angle) * r };
    });

    // 3. Remove cross-community edges so bridge nodes can't collapse everything to center
    const crossEdgeData = [];
    const edgesToDrop = [];
    graph.forEachEdge((edge, attr, source, target) => {
        const sc = graph.getNodeAttribute(source, "community") ?? 0;
        const tc = graph.getNodeAttribute(target, "community") ?? 0;
        if (sc !== tc) {
            crossEdgeData.push({ source, target, attr: { ...attr } });
            edgesToDrop.push(edge);
        }
    });
    for (const edge of edgesToDrop) { graph.dropEdge(edge); }

    // 4. Light FA2 within-community — just enough to pull connected nodes together
    //    within each cluster. Keep iterations LOW so isolated/singleton nodes (no
    //    within-community edges) don't drift far under pure repulsion with no gravity.
    lib.layoutForceAtlas2.assign(graph, {
        iterations: 120,
        settings: {
            scalingRatio: 2,
            gravity: 0.5,   // moderate gravity keeps singletons near their seed
            strongGravityMode: false,
            linLogMode: true,
            outboundAttractionDistribution: true,
            edgeWeightInfluence: settings.edgeWeightInfluence ?? 0.5,
            barnesHutOptimize: graph.order >= 1000,
            barnesHutTheta: 0.5,
            adjustSizes: false,
            slowDown: 3,
        }
    });

    // 5. Compute community centroids
    const centroidSum = new Map();
    const centroidCnt = new Map();
    graph.forEachNode((_, attr) => {
        const c = attr.community ?? 0;
        const s = centroidSum.get(c) ?? { x: 0, y: 0 };
        centroidSum.set(c, { x: s.x + attr.x, y: s.y + attr.y });
        centroidCnt.set(c, (centroidCnt.get(c) ?? 0) + 1);
    });
    const centroids = new Map();
    centroidSum.forEach((s, c) => {
        const n = centroidCnt.get(c);
        centroids.set(c, { x: s.x / n, y: s.y / n });
    });

    // 6. Explode: scale inter-centroid distances so clusters become separated islands
    const allC = [...centroids.values()];
    const gcx = allC.reduce((s, p) => s + p.x, 0) / allC.length;
    const gcy = allC.reduce((s, p) => s + p.y, 0) / allC.length;
    const EXPANSION = 3.0; // push clusters further apart (seed already spread them)
    const newCentroids = new Map();
    centroids.forEach((pos, c) => {
        newCentroids.set(c, {
            x: gcx + (pos.x - gcx) * EXPANSION,
            y: gcy + (pos.y - gcy) * EXPANSION,
        });
    });

    // 7. Translate every node by its cluster's centroid displacement (preserves internal shape)
    graph.updateEachNodeAttributes((_, attr) => {
        const c = attr.community ?? 0;
        const old = centroids.get(c) ?? { x: 0, y: 0 };
        const nw  = newCentroids.get(c) ?? { x: 0, y: 0 };
        return { ...attr, x: attr.x + (nw.x - old.x), y: attr.y + (nw.y - old.y) };
    });

    // 8. Restore cross-community edges for display (bridge lines between islands)
    for (const { source, target, attr } of crossEdgeData) {
        graph.addEdge(source, target, attr);
    }

    // 9. Center + light Noverlap for intra-cluster node overlap only
    centerLayout(graph);
    lib.layoutNoverlap.assign(graph, { margin: 2, expansion: 1.05, maxIterations: 80 });
}

/** Translate all node positions so their mean is at (0, 0). */
function centerLayout(graph) {
    let sumX = 0, sumY = 0, count = 0;
    graph.forEachNode((_node, attr) => {
        sumX += attr.x;
        sumY += attr.y;
        count++;
    });
    if (count === 0) return;
    const cx = sumX / count;
    const cy = sumY / count;
    graph.updateEachNodeAttributes((_node, attr) => ({
        ...attr,
        x: attr.x - cx,
        y: attr.y - cy,
    }));
}

/**
 * Refine layout from current positions (for live slider updates — no seed reset).
 */
function refineLayout(graph, settings) {
    const lib = window.graphologyLibrary;
    lib.layoutForceAtlas2.assign(graph, {
        iterations: settings.iterations ?? 200,
        settings: {
            scalingRatio: 5,
            gravity: settings.gravity ?? 0,
            strongGravityMode: false,
            linLogMode: true,
            outboundAttractionDistribution: true,
            edgeWeightInfluence: settings.edgeWeightInfluence ?? 0.5,
            barnesHutOptimize: graph.order >= 1000,
            barnesHutTheta: 0.5,
            adjustSizes: false,
            slowDown: Math.max(1, Math.log(graph.order)),
        }
    });
    centerLayout(graph);
}

/**
 * Color nodes by community cluster.
 * Louvain is run during runLayout; this function just maps community → color.
 */
function applyNodeColors(graph) {
    graph.updateEachNodeAttributes((_node, attr) => {
        const community = attr.community ?? 0;
        return { ...attr, color: COMMUNITY_PALETTE[community % COMMUNITY_PALETTE.length] };
    });
}

/**
 * Fit camera to show all nodes.
 *
 * Sigma 2.x normalises node coords using:
 *   normX = (x - xMin) / maxDim   where maxDim = max(xRange, yRange)
 *   normY = (y - yMin) / maxDim
 *
 * The bounding-box centre in normalised space is therefore:
 *   cx = (xRange / 2) / maxDim
 *   cy = (yRange / 2) / maxDim
 *
 * We compute this from the actual graph node positions so the camera
 * centres correctly regardless of aspect ratio.
 */
function fitCamera(sigmaInstance) {
    setTimeout(() => {
        const graph = sigmaInstance.getGraph();
        const xs = [], ys = [];
        graph.forEachNode((_, attr) => { xs.push(attr.x); ys.push(attr.y); });
        if (xs.length === 0) return;

        // Use 2nd–98th percentile to avoid outlier nodes stretching the bbox
        xs.sort((a, b) => a - b);
        ys.sort((a, b) => a - b);
        const trim = Math.max(1, Math.floor(xs.length * 0.02));
        const xMin = xs[trim], xMax = xs[xs.length - 1 - trim];
        const yMin = ys[trim], yMax = ys[ys.length - 1 - trim];

        const xRange = xMax - xMin;
        const yRange = yMax - yMin;
        const maxDim = Math.max(xRange, yRange, 1);

        // Camera x/y is the bounding-box centre in Sigma's normalised graph space
        sigmaInstance.getCamera().animate(
            { x: (xRange / 2) / maxDim, y: (yRange / 2) / maxDim, angle: 0, ratio: 1.4 },
            { duration: 600 }
        );
    }, 400);
}

export async function renderTopologyGraph(elementId, graphNodes, graphLinks, dotNetRef, layoutSettings) {
    await ensureScripts();

    const container = document.getElementById(elementId);
    if (!container) return;

    disposeTopologyGraph(elementId);

    const graph = new window.graphology.Graph();

    const state = {
        hoveredNode: null,
        hoveredNeighbors: null,
        searchQuery: "",
        selectedNode: null,
        suggestions: null
    };

    let maxObs = 1;
    for (const link of graphLinks) {
        if ((link.observations ?? 1) > maxObs) maxObs = link.observations;
    }

    graphNodes.forEach((n) => {
        graph.addNode(n.id, {
            x: 0,
            y: 0,
            // Bridge nodes slightly larger; regular nodes proportional to degree
            size: n.isBridge ? 7 : Math.max(3, Math.min(n.degree * 0.9, 7)),
            label: n.label,
            color: "#1C1917",
            isBridge: n.isBridge,
            degree: n.degree
        });
    });

    for (const l of graphLinks) {
        if (!graph.hasNode(l.source) || !graph.hasNode(l.target)) continue;
        if (graph.hasEdge(l.source, l.target) || graph.hasEdge(l.target, l.source)) continue;

        const physicsWeight = l.snrDb != null ? Math.max(0.1, l.snrDb + 35) : 1;

        graph.addEdge(l.source, l.target, {
            size: 1.2 + ((l.observations ?? 1) / maxObs) * 3.0,
            color: snrHex(l.snrDb),
            weight: physicsWeight,
            label: l.snrDb != null ? `${l.snrDb.toFixed(1)} dB` : ""
        });
    }

    runLayout(graph, layoutSettings);
    applyNodeColors(graph);

    const sigmaInstance = new window.Sigma(graph, container, {
        renderEdgeLabels: false,
        defaultNodeType: "circle",
        labelFont: "system-ui, -apple-system, sans-serif",
        labelWeight: "600",
        labelColor: { color: "#1e293b" },
        labelSize: 12,
        // Only render labels for nodes big enough to read at current zoom.
        // Users zoom in to see more labels.
        labelRenderedSizeThreshold: 4,
    });

    sigmaInstance.setSetting("nodeReducer", (node, data) => {
        const res = { ...data };

        if (state.hoveredNeighbors && !state.hoveredNeighbors.has(node) && state.hoveredNode !== node) {
            res.label = "";
            res.color = "#CBD5E1";
        }

        if (state.selectedNode === node) {
            res.highlighted = true;
            res.forceLabel = true;
        } else if (state.suggestions) {
            if (state.suggestions.has(node)) {
                res.forceLabel = true;
            } else {
                res.label = "";
                res.color = "#CBD5E1";
            }
        }
        return res;
    });

    sigmaInstance.setSetting("edgeReducer", (edge, data) => {
        const res = { ...data };

        if (state.hoveredNode && graph.source(edge) !== state.hoveredNode && graph.target(edge) !== state.hoveredNode) {
            res.hidden = true;
        }

        if (state.suggestions && (!state.suggestions.has(graph.source(edge)) || !state.suggestions.has(graph.target(edge)))) {
            res.hidden = true;
        }

        return res;
    });

    fitCamera(sigmaInstance);

    let draggedNode = null;
    let isDragging = false;

    sigmaInstance.on("downNode", (e) => {
        isDragging = true;
        draggedNode = e.node;
        sigmaInstance.getCamera().disable();
    });

    sigmaInstance.getMouseCaptor().on("mousemovebody", (e) => {
        if (!isDragging || !draggedNode) return;
        const pos = sigmaInstance.viewportToGraph(e);
        graph.setNodeAttribute(draggedNode, "x", pos.x);
        graph.setNodeAttribute(draggedNode, "y", pos.y);
    });

    sigmaInstance.getMouseCaptor().on("mouseup", () => {
        if (draggedNode) {
            isDragging = false;
            draggedNode = null;
            sigmaInstance.getCamera().enable();
        }
    });

    sigmaInstance.on("clickNode", (event) => {
        if (isDragging) return;
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("OnTopologyNodeClicked", event.node)
                .catch(err => console.error("[topology-graph] click error", err));
        }
    });

    sigmaInstance.on("enterNode", ({ node }) => {
        state.hoveredNode = node;
        state.hoveredNeighbors = new Set(graph.neighbors(node));
        sigmaInstance.refresh({ skipIndexation: true });
        container.style.cursor = "grab";
    });

    sigmaInstance.on("leaveNode", () => {
        state.hoveredNode = null;
        state.hoveredNeighbors = null;
        sigmaInstance.refresh({ skipIndexation: true });
        container.style.cursor = "default";
    });

    topologyInstances.set(elementId, { sigma: sigmaInstance, graph, state });
    // Expose for browser-console debugging
    window.__topologyDebug = topologyInstances;
}

export function searchTopologyNode(elementId, query) {
    const inst = topologyInstances.get(elementId);
    if (!inst) return;

    const { sigma, graph, state } = inst;
    state.searchQuery = query || "";

    if (state.searchQuery) {
        const lcQuery = state.searchQuery.toLowerCase();

        const suggestions = graph.nodes()
            .map((n) => ({ id: n, label: graph.getNodeAttribute(n, "label") }))
            .filter(({ label }) => label && label.toLowerCase().includes(lcQuery));

        if (suggestions.length === 1 && suggestions[0].label.toLowerCase() === lcQuery) {
            state.selectedNode = suggestions[0].id;
            state.suggestions = null;

            const nodePosition = sigma.getNodeDisplayData(state.selectedNode);
            sigma.getCamera().animate(
                { x: nodePosition.x, y: nodePosition.y, ratio: 0.12 },
                { duration: 500 }
            );
        } else {
            state.selectedNode = null;
            state.suggestions = new Set(suggestions.map((s) => s.id));
        }
    } else {
        state.selectedNode = null;
        state.suggestions = null;
    }

    sigma.refresh({ skipIndexation: true });
}

/** Re-run the full layout pipeline from a fresh circular seed. */
export function spreadTopologyLayout(elementId, settings) {
    const inst = topologyInstances.get(elementId);
    if (!inst) return;

    runLayout(inst.graph, settings);
    applyNodeColors(inst.graph);
    inst.sigma.refresh();
    fitCamera(inst.sigma);
}

/** Apply more FA2 iterations from current positions (used by live sliders). */
export function updateTopologyLayout(elementId, settings) {
    const inst = topologyInstances.get(elementId);
    if (!inst) return;

    refineLayout(inst.graph, settings);
    inst.sigma.refresh();
}

/** Manually trigger a Noverlap pass — useful after dragging nodes. */
export function applyNoverlapPass(elementId) {
    const inst = topologyInstances.get(elementId);
    if (!inst) return;

    window.graphologyLibrary.layoutNoverlap.assign(inst.graph, { margin: 6, expansion: 1.05, maxIterations: 80 });
    inst.sigma.refresh();
    fitCamera(inst.sigma);
}

/** Scroll the graph canvas into the centre of the viewport after render. */
export function scrollToGraph(elementId) {
    const el = document.getElementById(elementId);
    if (el) el.scrollIntoView({ behavior: "smooth", block: "center" });
}

export function disposeTopologyGraph(elementId) {
    const inst = topologyInstances.get(elementId);
    if (inst) {
        inst.sigma.kill();
        const container = document.getElementById(elementId);
        if (container) container.innerHTML = "";
        topologyInstances.delete(elementId);
    }
}
