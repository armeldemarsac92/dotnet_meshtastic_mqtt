const realtimeClientConstants = Object.freeze({
  keepAliveSeconds: 30,
  minimumRefreshDelayMs: 5_000,
  reconnectPeriodMs: 5_000,
  refreshSkewMs: 60_000
});

export function applyRealtimeSessionToOptions(session, options) {
  if (!session) {
    return;
  }

  options.clientId = session.clientId;
  options.username = session.clientId;
  options.password = session.token;
}

export function computeRealtimeSessionRefreshDelay(expiresAtUtc, nowMs = Date.now()) {
  if (!expiresAtUtc) {
    return realtimeClientConstants.minimumRefreshDelayMs;
  }

  const expiresAtMs = new Date(expiresAtUtc).getTime();

  if (Number.isNaN(expiresAtMs)) {
    throw new Error("The realtime session expiry is invalid.");
  }

  return Math.max(
    realtimeClientConstants.minimumRefreshDelayMs,
    expiresAtMs - nowMs - realtimeClientConstants.refreshSkewMs);
}

export function getRealtimeClientConstants() {
  return realtimeClientConstants;
}

export function normalizeRealtimeSession(session) {
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

export async function refreshRealtimeSessionState({
  currentSession,
  generation,
  isGenerationCurrent,
  notifySessionRefreshedAsync,
  refreshSessionAsync,
  requestReconnect
}) {
  if (!isGenerationCurrent(generation)) {
    return {
      reconnectRequested: false,
      session: currentSession
    };
  }

  const nextSession = normalizeRealtimeSession(await refreshSessionAsync());

  if (!isGenerationCurrent(generation)) {
    return {
      reconnectRequested: false,
      session: currentSession
    };
  }

  if (notifySessionRefreshedAsync) {
    await notifySessionRefreshedAsync(nextSession);
  }

  const reconnectRequested = shouldReconnectForRefreshedSession(currentSession, nextSession);
  if (reconnectRequested) {
    requestReconnect?.("Refreshing broker session.");
  }

  return {
    reconnectRequested,
    session: nextSession
  };
}

export function shouldReconnectForRefreshedSession(currentSession, nextSession) {
  if (!currentSession || !nextSession) {
    return false;
  }

  return currentSession.brokerUrl !== nextSession.brokerUrl
    || currentSession.clientId !== nextSession.clientId
    || currentSession.token !== nextSession.token;
}
