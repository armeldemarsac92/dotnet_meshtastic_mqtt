import test from "node:test";
import assert from "node:assert/strict";

import {
  computeRealtimeSessionRefreshDelay,
  getRealtimeClientConstants,
  normalizeRealtimeSession,
  refreshRealtimeSessionState
} from "../../src/MeshBoard.Client/wwwroot/js/realtimeClientCore.mjs";

const realtimeClientConstants = getRealtimeClientConstants();

test("normalizeRealtimeSession accepts API payload casing and deduplicates topics", () => {
  const session = normalizeRealtimeSession({
    AllowedTopicPatterns: ["meshboard/workspaces/a/live/#", "meshboard/workspaces/a/live/#", ""],
    BrokerUrl: "wss://broker.example.org/mqtt",
    ClientId: "meshboard-client-1",
    ExpiresAtUtc: "2026-03-19T10:30:00.000Z",
    Token: "jwt-token"
  });

  assert.deepEqual(session, {
    allowedTopicPatterns: ["meshboard/workspaces/a/live/#"],
    brokerUrl: "wss://broker.example.org/mqtt",
    clientId: "meshboard-client-1",
    expiresAtUtc: "2026-03-19T10:30:00.000Z",
    token: "jwt-token"
  });
});

test("computeRealtimeSessionRefreshDelay applies skew and minimum floor", () => {
  const nowMs = Date.parse("2026-03-19T10:00:00.000Z");

  assert.equal(
    computeRealtimeSessionRefreshDelay("2026-03-19T10:02:30.000Z", nowMs),
    90_000);

  assert.equal(
    computeRealtimeSessionRefreshDelay("2026-03-19T10:00:30.000Z", nowMs),
    realtimeClientConstants.minimumRefreshDelayMs);
});

test("refreshRealtimeSessionState requests reconnect when the refreshed token changes", async () => {
  const callbackCalls = [];
  const reconnectReasons = [];

  const refreshResult = await refreshRealtimeSessionState({
    currentSession: normalizeRealtimeSession({
      brokerUrl: "wss://broker.example.org/mqtt",
      clientId: "meshboard-client-1",
      token: "jwt-old",
      expiresAtUtc: "2026-03-19T10:15:00.000Z",
      allowedTopicPatterns: ["meshboard/workspaces/a/live/#"]
    }),
    generation: 4,
    isGenerationCurrent(candidateGeneration) {
      return candidateGeneration === 4;
    },
    async notifySessionRefreshedAsync(nextSession) {
      callbackCalls.push(nextSession);
    },
    async refreshSessionAsync() {
      return {
        brokerUrl: "wss://broker.example.org/mqtt",
        clientId: "meshboard-client-1",
        token: "jwt-new",
        expiresAtUtc: "2026-03-19T10:20:00.000Z",
        allowedTopicPatterns: ["meshboard/workspaces/a/live/#"]
      };
    },
    requestReconnect(reason) {
      reconnectReasons.push(reason);
    }
  });

  assert.equal(refreshResult.reconnectRequested, true);
  assert.equal(refreshResult.session.token, "jwt-new");
  assert.equal(callbackCalls.length, 1);
  assert.deepEqual(reconnectReasons, ["Refreshing broker session."]);
});

test("refreshRealtimeSessionState skips reconnect when the broker session is unchanged", async () => {
  const reconnectReasons = [];

  const refreshResult = await refreshRealtimeSessionState({
    currentSession: normalizeRealtimeSession({
      brokerUrl: "wss://broker.example.org/mqtt",
      clientId: "meshboard-client-1",
      token: "jwt-stable",
      expiresAtUtc: "2026-03-19T10:15:00.000Z",
      allowedTopicPatterns: ["meshboard/workspaces/a/live/#"]
    }),
    generation: 8,
    isGenerationCurrent(candidateGeneration) {
      return candidateGeneration === 8;
    },
    async notifySessionRefreshedAsync() {
    },
    async refreshSessionAsync() {
      return {
        brokerUrl: "wss://broker.example.org/mqtt",
        clientId: "meshboard-client-1",
        token: "jwt-stable",
        expiresAtUtc: "2026-03-19T10:25:00.000Z",
        allowedTopicPatterns: ["meshboard/workspaces/a/live/#"]
      };
    },
    requestReconnect(reason) {
      reconnectReasons.push(reason);
    }
  });

  assert.equal(refreshResult.reconnectRequested, false);
  assert.equal(reconnectReasons.length, 0);
});

test("refreshRealtimeSessionState ignores stale generations before refreshing", async () => {
  let refreshCalls = 0;

  const refreshResult = await refreshRealtimeSessionState({
    currentSession: normalizeRealtimeSession({
      brokerUrl: "wss://broker.example.org/mqtt",
      clientId: "meshboard-client-1",
      token: "jwt-stable",
      expiresAtUtc: "2026-03-19T10:15:00.000Z",
      allowedTopicPatterns: ["meshboard/workspaces/a/live/#"]
    }),
    generation: 3,
    isGenerationCurrent() {
      return false;
    },
    async notifySessionRefreshedAsync() {
    },
    async refreshSessionAsync() {
      refreshCalls += 1;
      return {
        brokerUrl: "wss://broker.example.org/mqtt",
        clientId: "meshboard-client-1",
        token: "jwt-next",
        expiresAtUtc: "2026-03-19T10:20:00.000Z",
        allowedTopicPatterns: ["meshboard/workspaces/a/live/#"]
      };
    },
    requestReconnect() {
    }
  });

  assert.equal(refreshCalls, 0);
  assert.equal(refreshResult.reconnectRequested, false);
  assert.equal(refreshResult.session.token, "jwt-stable");
});
