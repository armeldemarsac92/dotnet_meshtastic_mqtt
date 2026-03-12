import mqtt from "../vendor/mqtt.esm.js";

const refreshSkewMs = 60_000;
const reconnectPeriodMs = 5_000;
const keepAliveSeconds = 30;

let currentClient = null;
let currentCallbacks = null;
let currentGeneration = 0;
let currentSession = null;
let refreshTimerHandle = null;
let disconnectReason = null;

export async function connect(session, callbacks) {
  if (!callbacks) {
    throw new Error("A .NET callback reference is required for realtime interop.");
  }

  await disconnect();

  currentGeneration += 1;
  const generation = currentGeneration;
  currentCallbacks = callbacks;
  currentSession = normalizeSession(session);
  disconnectReason = null;

  const client = mqtt.connect(currentSession.brokerUrl, buildOptions(generation));
  currentClient = client;
  wireClient(client, generation);
  scheduleSessionRefresh(generation);

  try {
    await waitForInitialConnect(client);
  } catch (error) {
    disconnectReason = "The realtime connection failed to start.";
    await disconnect();
    throw error;
  }
}

export async function disconnect() {
  clearRefreshTimer();

  if (!currentClient) {
    disconnectReason = "Disconnected locally.";
    return;
  }

  const client = currentClient;
  currentClient = null;
  disconnectReason = "Disconnected locally.";

  try {
    await client.endAsync(true);
  } finally {
    clearRefreshTimer();
  }
}

function applySessionToOptions(options) {
  if (!currentSession) {
    return;
  }

  options.clientId = currentSession.clientId;
  options.username = currentSession.clientId;
  options.password = currentSession.token;
}

function buildOptions(generation) {
  return {
    clean: true,
    clientId: currentSession.clientId,
    connectTimeout: 15_000,
    keepalive: keepAliveSeconds,
    password: currentSession.token,
    protocolId: "MQTT",
    protocolVersion: 5,
    queueQoSZero: false,
    reconnectOnConnackError: true,
    reconnectPeriod: reconnectPeriodMs,
    resubscribe: false,
    transformWsUrl(url, options) {
      if (generation !== currentGeneration || !currentSession) {
        return url;
      }

      applySessionToOptions(options);
      return currentSession.brokerUrl ?? url;
    },
    username: currentSession.clientId
  };
}

function clearRefreshTimer() {
  if (refreshTimerHandle === null) {
    return;
  }

  clearTimeout(refreshTimerHandle);
  refreshTimerHandle = null;
}

async function emitConnected(subscriptionCount) {
  if (!currentCallbacks) {
    return;
  }

  await currentCallbacks.invokeMethodAsync("HandleConnectedAsync", {
    connectedAtUtc: new Date().toISOString(),
    subscriptionCount
  });
}

async function emitDisconnected(reason) {
  if (!currentCallbacks) {
    return;
  }

  await currentCallbacks.invokeMethodAsync("HandleDisconnectedAsync", {
    disconnectedAtUtc: new Date().toISOString(),
    reason
  });
}

async function emitError(error) {
  if (!currentCallbacks) {
    return;
  }

  const message = normalizeErrorMessage(error);
  await currentCallbacks.invokeMethodAsync("HandleTransportErrorAsync", message);
}

async function emitMessage(topic, payload) {
  if (!currentCallbacks) {
    return;
  }

  await currentCallbacks.invokeMethodAsync("HandleMessageAsync", {
    payloadSizeBytes: payload?.byteLength ?? 0,
    receivedAtUtc: new Date().toISOString(),
    topic
  });
}

async function emitReconnecting() {
  if (!currentCallbacks) {
    return;
  }

  await currentCallbacks.invokeMethodAsync("HandleReconnectingAsync");
}

function normalizeErrorMessage(error) {
  if (!error) {
    return "The realtime broker connection reported an error.";
  }

  if (typeof error === "string") {
    return error.trim();
  }

  if (typeof error.message === "string" && error.message.trim().length > 0) {
    return error.message.trim();
  }

  return "The realtime broker connection reported an error.";
}

function normalizeSession(session) {
  const brokerUrl = session?.brokerUrl ?? session?.BrokerUrl;
  const clientId = session?.clientId ?? session?.ClientId;
  const token = session?.token ?? session?.Token;
  const expiresAtUtc = session?.expiresAtUtc ?? session?.ExpiresAtUtc;
  const allowedTopicPatterns = session?.allowedTopicPatterns ?? session?.AllowedTopicPatterns ?? [];

  if (!brokerUrl || !clientId || !token || !expiresAtUtc) {
    throw new Error("The realtime session payload is incomplete.");
  }

  return {
    allowedTopicPatterns: [...new Set(allowedTopicPatterns.filter(Boolean))],
    brokerUrl,
    clientId,
    expiresAtUtc,
    token
  };
}

async function refreshSession(generation) {
  if (generation !== currentGeneration || !currentCallbacks) {
    return;
  }

  const nextSession = normalizeSession(await currentCallbacks.invokeMethodAsync("RefreshSessionAsync"));
  currentSession = nextSession;
  await currentCallbacks.invokeMethodAsync("HandleSessionRefreshedAsync", nextSession);
  scheduleSessionRefresh(generation);
}

async function refreshSessionSafely(generation) {
  try {
    await refreshSession(generation);
  } catch (error) {
    await emitError(`Realtime token refresh failed: ${normalizeErrorMessage(error)}`);
  }
}

function scheduleSessionRefresh(generation) {
  clearRefreshTimer();

  if (generation !== currentGeneration || !currentSession?.expiresAtUtc) {
    return;
  }

  const expiresAtMs = new Date(currentSession.expiresAtUtc).getTime();
  const delayMs = Math.max(5_000, expiresAtMs - Date.now() - refreshSkewMs);

  refreshTimerHandle = setTimeout(() => {
    refreshTimerHandle = null;
    void refreshSessionSafely(generation);
  }, delayMs);
}

async function subscribeAuthorizedTopics(client, generation) {
  if (generation !== currentGeneration || !currentSession) {
    return 0;
  }

  const topics = currentSession.allowedTopicPatterns;
  if (!topics.length) {
    return 0;
  }

  const topicMap = Object.fromEntries(topics.map(topic => [topic, { qos: 0 }]));
  await client.subscribeAsync(topicMap);
  return topics.length;
}

function waitForInitialConnect(client) {
  return new Promise((resolve, reject) => {
    const handleConnect = () => {
      cleanup();
      resolve();
    };

    const handleError = error => {
      cleanup();
      reject(error ?? new Error("The realtime connection failed before CONNECT completed."));
    };

    const handleClose = () => {
      cleanup();
      reject(new Error("The realtime connection closed before CONNECT completed."));
    };

    const cleanup = () => {
      client.off("connect", handleConnect);
      client.off("error", handleError);
      client.off("close", handleClose);
    };

    client.on("connect", handleConnect);
    client.on("error", handleError);
    client.on("close", handleClose);
  });
}

function wireClient(client, generation) {
  client.on("connect", async () => {
    if (generation !== currentGeneration) {
      return;
    }

    try {
      const subscriptionCount = await subscribeAuthorizedTopics(client, generation);
      await emitConnected(subscriptionCount);
    } catch (error) {
      await emitError(error);
    }
  });

  client.on("message", (topic, payload) => {
    if (generation !== currentGeneration) {
      return;
    }

    void emitMessage(topic, payload);
  });

  client.on("reconnect", () => {
    if (generation !== currentGeneration) {
      return;
    }

    void emitReconnecting();
  });

  client.on("error", error => {
    if (generation !== currentGeneration) {
      return;
    }

    void emitError(error);
  });

  client.on("end", () => {
    if (generation !== currentGeneration) {
      return;
    }

    currentClient = null;
    clearRefreshTimer();
    void emitDisconnected(disconnectReason ?? "The realtime broker connection closed.");
    disconnectReason = null;
  });
}
