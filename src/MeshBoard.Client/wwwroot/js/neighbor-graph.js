const FORCE_GRAPH_SCRIPT_ID = "meshboard-force-graph-script";
const FORCE_GRAPH_SCRIPT_URL = "https://unpkg.com/force-graph/dist/force-graph.min.js";

let forceGraphLoadPromise = null;
const graphInstances = new Map();

function ensureForceGraphScript() {
    if (forceGraphLoadPromise) return forceGraphLoadPromise;

    const existing = document.getElementById(FORCE_GRAPH_SCRIPT_ID);
    if (existing) {
        forceGraphLoadPromise = window.ForceGraph
            ? Promise.resolve()
            : new Promise((resolve, reject) => {
                existing.addEventListener("load", resolve, { once: true });
                existing.addEventListener("error", () => {
                    forceGraphLoadPromise = null;
                    reject(new Error("Failed to load force-graph"));
                }, { once: true });
            });
        return forceGraphLoadPromise;
    }

    forceGraphLoadPromise = new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.id = FORCE_GRAPH_SCRIPT_ID;
        script.src = FORCE_GRAPH_SCRIPT_URL;
        script.onload = resolve;
        script.onerror = () => {
            forceGraphLoadPromise = null;
            reject(new Error("Failed to load force-graph"));
        };
        document.head.appendChild(script);
    });

    return forceGraphLoadPromise;
}

// SNR → RGBA helper matching the frontend color palette
function snrRgba(snrDb, alpha) {
    if (snrDb == null) return `rgba(120,113,108,${alpha})`;  // stone-500
    if (snrDb > 0)    return `rgba(22,163,74,${alpha})`;    // green-700
    if (snrDb >= -10) return `rgba(202,138,4,${alpha})`;    // amber-600
    return `rgba(220,38,38,${alpha})`;                       // red-600
}

function snrLabel(snrDb) {
    return snrDb != null ? `${snrDb.toFixed(1)} dB` : null;
}

function shortLabel(neighbor) {
    const name = neighbor.displayName?.trim();
    if (name) return name.length > 9 ? name.slice(0, 9) : name;
    const id = neighbor.nodeId ?? "";
    return id.length >= 4 ? id.slice(-4) : id;
}

function roundedRect(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.lineTo(x + w - r, y);
    ctx.quadraticCurveTo(x + w, y, x + w, y + r);
    ctx.lineTo(x + w, y + h - r);
    ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
    ctx.lineTo(x + r, y + h);
    ctx.quadraticCurveTo(x, y + h, x, y + h - r);
    ctx.lineTo(x, y + r);
    ctx.quadraticCurveTo(x, y, x + r, y);
    ctx.closePath();
}

function drawNode(node, ctx, globalScale) {
    if (!isFinite(node.x) || !isFinite(node.y)) return;

    const w = node.isCentral ? 72 : 62;
    const h = node.isCentral ? 34 : 28;
    const r = h / 2;

    node.__w = w;
    node.__h = h;

    const x = node.x - w / 2;
    const y = node.y - h / 2;

    if (node.isCentral) {
        // Dark pill — matches the cinder-950 chip style from the UI
        ctx.save();
        ctx.shadowColor = "rgba(28,25,23,0.20)";
        ctx.shadowBlur = 12 / globalScale;
        ctx.shadowOffsetY = 3 / globalScale;
        roundedRect(ctx, x, y, w, h, r);
        ctx.fillStyle = "rgba(28,25,23,0.90)";
        ctx.fill();
        ctx.restore();

        const fontSize = Math.max(8, 12 / globalScale);
        ctx.font = `600 ${fontSize}px ui-sans-serif,system-ui,sans-serif`;
        ctx.fillStyle = "rgba(255,255,255,0.95)";
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";
        ctx.fillText(node.label, node.x, node.y);
    } else {
        // White glass pill with SNR-colored border — matches shell-subpanel item style
        ctx.save();
        ctx.shadowColor = "rgba(28,25,23,0.07)";
        ctx.shadowBlur = 6 / globalScale;
        ctx.shadowOffsetY = 2 / globalScale;
        roundedRect(ctx, x, y, w, h, r);
        ctx.fillStyle = "rgba(255,255,255,0.93)";
        ctx.fill();
        ctx.restore();

        roundedRect(ctx, x, y, w, h, r);
        ctx.strokeStyle = node.borderColor;
        ctx.lineWidth = 1.5 / globalScale;
        ctx.stroke();

        const fontSize = Math.max(7, 10 / globalScale);
        ctx.font = `500 ${fontSize}px ui-sans-serif,system-ui,sans-serif`;
        ctx.fillStyle = "#3c3028";
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";
        ctx.fillText(node.label, node.x, node.y);
    }
}

function drawLink(link, ctx, globalScale) {
    if (!link.source?.x) return;

    const label = link.snrLabel;
    if (!label) return; // Skip label when SNR unknown

    const fontSize = Math.max(6, 8 / globalScale);
    const mx = (link.source.x + link.target.x) / 2;
    const my = (link.source.y + link.target.y) / 2;

    ctx.font = `${fontSize}px ui-sans-serif,system-ui,sans-serif`;
    const tw = ctx.measureText(label).width;
    const th = fontSize;
    const pad = 3 / globalScale;
    const bw = tw + pad * 2;
    const bh = th + pad * 2;
    const br = bh / 2; // Fully rounded pill
    const bx = mx - bw / 2;
    const by = my - bh / 2;

    // Sand-colored pill background — matches bg-sand-100 from the UI
    ctx.fillStyle = "rgba(245,241,235,0.96)";
    roundedRect(ctx, bx, by, bw, bh, br);
    ctx.fill();

    ctx.fillStyle = link.labelColor;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(label, mx, my);
}

export async function renderNeighborGraph(elementId, centralId, centralLabel, neighbors, dotNetRef) {
    await ensureForceGraphScript();

    const container = document.getElementById(elementId);
    if (!container) return;

    disposeNeighborGraph(elementId);

    const nodes = [
        { id: centralId, label: centralLabel, isCentral: true }
    ];
    const links = [];

    for (const n of neighbors) {
        const borderColor = snrRgba(n.snrDb, 0.65);
        const linkLineColor = snrRgba(n.snrDb, 0.25);
        const labelColor = snrRgba(n.snrDb, 1.0);

        nodes.push({
            id: n.nodeId,
            label: shortLabel(n),
            isCentral: false,
            borderColor
        });
        links.push({
            source: centralId,
            target: n.nodeId,
            lineColor: linkLineColor,
            labelColor,
            snrLabel: snrLabel(n.snrDb)
        });
    }

    const fg = window.ForceGraph()(container)
        .width(container.clientWidth)
        .height(container.clientHeight)
        .graphData({ nodes, links })
        .nodeId("id")
        .backgroundColor("rgba(0,0,0,0)")
        .linkColor(link => link.lineColor)
        .linkWidth(1.2)
        .linkDirectionalArrowLength(0)
        .nodeCanvasObject(drawNode)
        .nodePointerAreaPaint((node, color, ctx) => {
            const w = node.__w ?? 62;
            const h = node.__h ?? 28;
            const x = node.x - w / 2;
            const y = node.y - h / 2;
            ctx.fillStyle = color;
            roundedRect(ctx, x, y, w, h, h / 2);
            ctx.fill();
        })
        .linkCanvasObjectMode(() => "after")
        .linkCanvasObject(drawLink)
        .d3AlphaDecay(0.02)
        .d3VelocityDecay(0.35)
        .onNodeHover(node => {
            container.style.cursor = (node && !node.isCentral) ? "pointer" : "default";
        })
        .onNodeClick(node => {
            if (!node.isCentral && dotNetRef) {
                dotNetRef.invokeMethodAsync("OnGraphNodeClicked", node.id)
                    .catch(err => console.error("[neighbor-graph] invokeMethodAsync error", err));
            }
        });

    const chargeForce = fg.d3Force("charge");
    if (chargeForce?.strength) chargeForce.strength(-350);

    const linkForce = fg.d3Force("link");
    if (linkForce?.distance) linkForce.distance(120);

    fg.d3ReheatSimulation();
    fg.onEngineStop(() => fg.zoomToFit(400, 36));

    const canvas = container.querySelector("canvas");
    if (canvas) canvas.style.background = "transparent";

    graphInstances.set(elementId, fg);
}

export function disposeNeighborGraph(elementId) {
    const fg = graphInstances.get(elementId);
    if (fg) {
        try { fg._destructor?.(); } catch (_) {}
        const container = document.getElementById(elementId);
        if (container) container.innerHTML = "";
        graphInstances.delete(elementId);
    }
}
