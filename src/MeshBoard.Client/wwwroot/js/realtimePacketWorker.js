const textDecoder = new TextDecoder();

self.onmessage = event => {
  const requestId = event?.data?.requestId;
  const request = event?.data?.request;

  try {
    const result = processPacket(request);
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
