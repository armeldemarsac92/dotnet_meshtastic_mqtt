let worker;
let nextRequestId = 0;
const pendingRequests = new Map();

export function dispose() {
  if (!worker) {
    return;
  }

  for (const pending of pendingRequests.values()) {
    pending.reject(new Error("The realtime packet worker was disposed before completing the request."));
  }

  pendingRequests.clear();
  worker.terminate();
  worker = null;
}

export function processPacket(request) {
  if (!request) {
    return Promise.reject(new Error("The realtime packet worker request is missing."));
  }

  const requestId = ++nextRequestId;
  const activeWorker = getWorker();

  return new Promise((resolve, reject) => {
    pendingRequests.set(requestId, { resolve, reject });
    activeWorker.postMessage({ requestId, request });
  });
}

function getWorker() {
  if (worker) {
    return worker;
  }

  worker = new Worker(new URL("./realtimePacketWorker.js", import.meta.url), { type: "module" });
  worker.onmessage = handleMessage;
  worker.onerror = handleWorkerError;
  worker.onmessageerror = () => rejectAll(new Error("The realtime packet worker returned an unreadable message."));
  return worker;
}

function handleMessage(event) {
  const requestId = event?.data?.requestId;
  if (!requestId || !pendingRequests.has(requestId)) {
    return;
  }

  const pending = pendingRequests.get(requestId);
  pendingRequests.delete(requestId);
  pending.resolve(event.data.result);
}

function handleWorkerError(event) {
  const detail = typeof event?.message === "string" && event.message.trim().length > 0
    ? event.message.trim()
    : "The realtime packet worker failed unexpectedly.";

  rejectAll(new Error(detail));
}

function rejectAll(error) {
  for (const pending of pendingRequests.values()) {
    pending.reject(error);
  }

  pendingRequests.clear();

  if (worker) {
    worker.terminate();
    worker = null;
  }
}
