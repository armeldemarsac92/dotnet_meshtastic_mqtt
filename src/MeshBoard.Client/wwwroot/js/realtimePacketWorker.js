const textDecoder = new TextDecoder();
let currentKeyRecords = [];

self.onmessage = event => {
  const requestId = event?.data?.requestId;
  const kind = event?.data?.kind;
  const payload = event?.data?.payload;

  try {
    const result = dispatch(kind, payload);
    self.postMessage({ requestId, result });
  } catch (error) {
    self.postMessage({
      requestId,
      result: {
        isSuccess: false,
        decryptResultClassification: "NotAttempted",
        failureClassification: "MalformedPayload",
        errorDetail: normalizeErrorMessage(error),
        rawPacket: null
      }
    });
  }
};

function dispatch(kind, payload) {
  switch (kind) {
    case "processPacket":
      return processPacket(payload);
    case "replaceKeyRecords":
      return replaceKeyRecords(payload);
    case "clearKeyRecords":
      return clearKeyRecords();
    default:
      return createFailure("MalformedPayload", "The realtime packet worker request kind is not supported.");
  }
}

function processPacket(request) {
  if (!request || typeof request !== "object") {
    return createFailure("MalformedPayload", "The realtime packet request is missing.");
  }

  const downstreamTopic = normalizeText(request.downstreamTopic);
  const receivedAtUtc = normalizeText(request.receivedAtUtc) ?? new Date().toISOString();
  const payloadBase64 = normalizeText(request.payloadBase64);

  if (!payloadBase64) {
    return createFailure("MalformedPayload", "The realtime packet payload is missing.");
  }

  const envelope = parseEnvelope(payloadBase64);
  if (!envelope) {
    return createFailure("MalformedPayload", "The downstream packet envelope is invalid.");
  }

  const workspaceId = normalizeText(envelope.workspaceId);
  const brokerServer = normalizeText(envelope.brokerServer);
  const sourceTopic = normalizeText(envelope.topic);
  const sourcePayloadBase64 = normalizeText(envelope.payload);

  if (!workspaceId || !brokerServer || !sourceTopic || !sourcePayloadBase64) {
    return createFailure("MalformedPayload", "The downstream packet envelope is incomplete.");
  }

  const sourcePayloadBytes = base64ToBytes(sourcePayloadBase64);
  if (!sourcePayloadBytes) {
    return createFailure("MalformedPayload", "The raw packet payload is not valid base64.");
  }

  return {
    isSuccess: true,
    decryptResultClassification: "NotAttempted",
    failureClassification: null,
    errorDetail: null,
    rawPacket: {
      workspaceId,
      brokerServer,
      sourceTopic,
      downstreamTopic: downstreamTopic ?? "",
      payloadBase64: sourcePayloadBase64,
      payloadSizeBytes: sourcePayloadBytes.length,
      receivedAtUtc: normalizeText(envelope.receivedAtUtc) ?? receivedAtUtc
    }
  };
}

function replaceKeyRecords(payload) {
  if (!Array.isArray(payload)) {
    return createFailure("MalformedPayload", "The realtime key ring payload is invalid.");
  }

  currentKeyRecords = payload
    .filter(record => record && typeof record === "object")
    .map(record => ({
      id: normalizeText(record.id) ?? "",
      name: normalizeText(record.name) ?? "",
      topicPattern: normalizeText(record.topicPattern) ?? "",
      brokerServerProfileId: normalizeText(record.brokerServerProfileId),
      normalizedKeyBase64: normalizeText(record.normalizedKeyBase64) ?? "",
      keyLengthBytes: Number.isFinite(record.keyLengthBytes) ? Number(record.keyLengthBytes) : 0
    }))
    .filter(record => record.id && record.topicPattern && record.normalizedKeyBase64);

  return null;
}

function clearKeyRecords() {
  currentKeyRecords = [];
  return null;
}

function createFailure(failureClassification, errorDetail) {
  return {
    isSuccess: false,
    decryptResultClassification: "NotAttempted",
    failureClassification,
    errorDetail,
    rawPacket: null
  };
}

function parseEnvelope(payloadBase64) {
  const payloadBytes = base64ToBytes(payloadBase64);
  if (!payloadBytes) {
    return null;
  }

  try {
    return JSON.parse(textDecoder.decode(payloadBytes));
  } catch {
    return null;
  }
}

function base64ToBytes(value) {
  if (typeof value !== "string" || value.trim().length === 0) {
    return null;
  }

  try {
    const binary = atob(value);
    return Uint8Array.from(binary, character => character.charCodeAt(0));
  } catch {
    return null;
  }
}

function normalizeErrorMessage(error) {
  if (typeof error === "string" && error.trim().length > 0) {
    return error.trim();
  }

  if (error && typeof error.message === "string" && error.message.trim().length > 0) {
    return error.message.trim();
  }

  return "The realtime packet worker failed unexpectedly.";
}

function normalizeText(value) {
  return typeof value === "string" && value.trim().length > 0
    ? value.trim()
    : null;
}
