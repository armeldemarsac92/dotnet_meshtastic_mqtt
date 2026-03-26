import {
  getRealtimePacketWorkerConstants,
  normalizeKeyRecords,
  processPacketRequest
} from "./realtimePacketWorkerCore.mjs";

let currentKeyRecords = [];
const workerConstants = getRealtimePacketWorkerConstants();

self.onmessage = async event => {
  const requestId = event?.data?.requestId;
  const kind = event?.data?.kind;
  const payload = event?.data?.payload;

  try {
    const result = await dispatch(kind, payload);
    self.postMessage({ requestId, result });
  } catch (error) {
    self.postMessage({
      requestId,
      result: {
        isSuccess: false,
        decryptResultClassification: workerConstants.decryptResultClassifications.notAttempted,
        failureClassification: workerConstants.failureKinds.malformedPayload,
        errorDetail: normalizeErrorMessage(error),
        rawPacket: null
      }
    });
  }
};

function dispatch(kind, payload) {
  switch (kind) {
    case "processPacket":
      return processPacketRequest(payload, currentKeyRecords);
    case "replaceKeyRecords":
      return replaceKeyRecords(payload);
    case "clearKeyRecords":
      return clearKeyRecords();
    default:
      return createFailure(
        workerConstants.failureKinds.malformedPayload,
        "The realtime packet worker request kind is not supported.");
  }
}

function replaceKeyRecords(payload) {
  if (!Array.isArray(payload)) {
    return createFailure(
      workerConstants.failureKinds.malformedPayload,
      "The realtime key ring payload is invalid.");
  }

  currentKeyRecords = normalizeKeyRecords(payload);
  return null;
}

function clearKeyRecords() {
  currentKeyRecords = [];
  return null;
}

function createFailure(failureClassification, errorDetail) {
  return {
    isSuccess: false,
    decryptResultClassification: workerConstants.decryptResultClassifications.notAttempted,
    failureClassification,
    errorDetail,
    rawPacket: null
  };
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
