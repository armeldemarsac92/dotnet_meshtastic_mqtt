import test from "node:test";
import assert from "node:assert/strict";

import {
  getRealtimePacketWorkerConstants,
  normalizeKeyRecords,
  processPacketRequest
} from "../../src/MeshBoard.Client/wwwroot/js/realtimePacketWorkerCore.mjs";

const workerConstants = getRealtimePacketWorkerConstants();

test("processPacketRequest returns no matching key when encrypted packet has no local candidate", async () => {
  const topic = "msh/US/2/e/LongFast/!abcd1234";
  const keyBytes = new Uint8Array([0xd4, 0xf1, 0xbb, 0x3a, 0x20, 0x29, 0x07, 0x59, 0xf0, 0xbc, 0xff, 0xab, 0xcf, 0x4e, 0x69, 0x01]);
  const packetBytes = await createEncryptedMeshPacket({
    fromNodeNumber: 777,
    packetId: 91,
    keyBytes,
    dataBytes: encodeDataMessage()
  });

  const result = await processPacketRequest(
    createWorkerRequest(topic, packetBytes),
    normalizeKeyRecords([]));

  assert.equal(result.isSuccess, true);
  assert.equal(result.decryptResultClassification, workerConstants.decryptResultClassifications.encryptedButNotDecrypted);
  assert.equal(result.failureClassification, workerConstants.failureKinds.noMatchingKey);
  assert.equal(result.rawPacket?.isEncrypted, true);
  assert.equal(result.rawPacket?.decryptionAttempted, false);
  assert.equal(result.decodedPacket, null);
});

test("processPacketRequest returns protobuf parse failure when all candidate keys are wrong", async () => {
  const topic = "msh/US/2/e/LongFast/!abcd1234";
  const correctKey = new Uint8Array([0xd4, 0xf1, 0xbb, 0x3a, 0x20, 0x29, 0x07, 0x59, 0xf0, 0xbc, 0xff, 0xab, 0xcf, 0x4e, 0x69, 0x01]);
  const wrongKey = new Uint8Array([0x14, 0xf1, 0xbb, 0x3a, 0x20, 0x29, 0x07, 0x59, 0xf0, 0xbc, 0xff, 0xab, 0xcf, 0x4e, 0x69, 0x01]);
  const packetBytes = await createEncryptedMeshPacket({
    fromNodeNumber: 777,
    packetId: 91,
    keyBytes: correctKey,
    dataBytes: encodeDataMessage()
  });

  const result = await processPacketRequest(
    createWorkerRequest(topic, packetBytes),
    normalizeKeyRecords([
      {
        id: "wrong-key",
        name: "Wrong",
        topicPattern: "msh/US/2/e/LongFast/#",
        normalizedKeyBase64: bytesToBase64(wrongKey),
        keyLengthBytes: wrongKey.length
      }
    ]));

  assert.equal(result.isSuccess, true);
  assert.equal(result.decryptResultClassification, workerConstants.decryptResultClassifications.encryptedButNotDecrypted);
  assert.equal(result.failureClassification, workerConstants.failureKinds.protobufParseFailure);
  assert.equal(result.rawPacket?.decryptionAttempted, true);
  assert.equal(result.rawPacket?.decryptionSucceeded, false);
  assert.equal(result.decodedPacket, null);
});

test("processPacketRequest returns decrypted payload metadata when a matching key succeeds", async () => {
  const topic = "msh/US/2/e/LongFast/!abcd1234";
  const keyBytes = new Uint8Array([0xd4, 0xf1, 0xbb, 0x3a, 0x20, 0x29, 0x07, 0x59, 0xf0, 0xbc, 0xff, 0xab, 0xcf, 0x4e, 0x69, 0x01]);
  const dataBytes = encodeDataMessage();
  const packetBytes = await createEncryptedMeshPacket({
    fromNodeNumber: 777,
    packetId: 91,
    keyBytes,
    dataBytes
  });

  const result = await processPacketRequest(
    createWorkerRequest(topic, packetBytes),
    normalizeKeyRecords([
      {
        id: "matching-key",
        name: "LongFast",
        topicPattern: "msh/US/2/e/LongFast/#",
        normalizedKeyBase64: bytesToBase64(keyBytes),
        keyLengthBytes: keyBytes.length
      }
    ]));

  assert.equal(result.isSuccess, true);
  assert.equal(result.decryptResultClassification, workerConstants.decryptResultClassifications.decrypted);
  assert.equal(result.failureClassification, null);
  assert.equal(result.rawPacket?.decryptionAttempted, true);
  assert.equal(result.rawPacket?.decryptionSucceeded, true);
  assert.equal(result.rawPacket?.matchedKeyId, "matching-key");
  assert.equal(result.rawPacket?.decryptedPayloadBase64, bytesToBase64(dataBytes));
  assert.equal(result.rawPacket?.packetId, 91);
  assert.equal(result.rawPacket?.fromNodeNumber, 777);
  assert.equal(result.decodedPacket?.portNumValue, 1);
  assert.equal(result.decodedPacket?.portNumName, "TEXT_MESSAGE_APP");
  assert.equal(result.decodedPacket?.packetType, "Text Message");
  assert.equal(result.decodedPacket?.payloadPreview, "hello mesh");
  assert.equal(result.decodedPacket?.sourceNodeNumber, 5678);
  assert.equal(result.decodedPacket?.destinationNodeNumber, 1234);
});

test("processPacketRequest returns decoded packet metadata for direct decoded mesh payloads", async () => {
  const packetBytes = createDecodedMeshPacket({
    fromNodeNumber: 777,
    packetId: 91,
    dataBytes: encodeDataMessage()
  });

  const result = await processPacketRequest(
    createWorkerRequest("msh/US/2/e/LongFast/!abcd1234", packetBytes),
    normalizeKeyRecords([]));

  assert.equal(result.isSuccess, true);
  assert.equal(result.decryptResultClassification, workerConstants.decryptResultClassifications.notAttempted);
  assert.equal(result.failureClassification, null);
  assert.equal(result.rawPacket?.isEncrypted, false);
  assert.equal(result.rawPacket?.decryptionAttempted, false);
  assert.equal(result.decodedPacket?.packetType, "Text Message");
  assert.equal(result.decodedPacket?.payloadPreview, "hello mesh");
});

test("processPacketRequest returns unsupported port classification when the data wrapper is valid but unmapped", async () => {
  const topic = "msh/US/2/e/LongFast/!abcd1234";
  const keyBytes = new Uint8Array([0xd4, 0xf1, 0xbb, 0x3a, 0x20, 0x29, 0x07, 0x59, 0xf0, 0xbc, 0xff, 0xab, 0xcf, 0x4e, 0x69, 0x01]);
  const packetBytes = await createEncryptedMeshPacket({
    fromNodeNumber: 777,
    packetId: 91,
    keyBytes,
    dataBytes: encodeDataMessage({ portNumValue: 999, text: "unsupported" })
  });

  const result = await processPacketRequest(
    createWorkerRequest(topic, packetBytes),
    normalizeKeyRecords([
      {
        id: "matching-key",
        name: "LongFast",
        topicPattern: "msh/US/2/e/LongFast/#",
        normalizedKeyBase64: bytesToBase64(keyBytes),
        keyLengthBytes: keyBytes.length
      }
    ]));

  assert.equal(result.isSuccess, true);
  assert.equal(result.decryptResultClassification, workerConstants.decryptResultClassifications.decrypted);
  assert.equal(result.failureClassification, workerConstants.failureKinds.unsupportedPortNum);
  assert.equal(result.rawPacket?.decryptionSucceeded, true);
  assert.equal(result.decodedPacket, null);
});

test("processPacketRequest returns malformed payload when the downstream envelope is invalid", async () => {
  const result = await processPacketRequest(
    {
      downstreamTopic: "meshboard/workspaces/workspace-a/live/packets",
      payloadBase64: bytesToBase64(new Uint8Array([1, 2, 3])),
      receivedAtUtc: "2026-03-14T17:00:00Z"
    },
    []);

  assert.equal(result.isSuccess, false);
  assert.equal(result.failureClassification, workerConstants.failureKinds.malformedPayload);
  assert.equal(result.rawPacket, null);
});

function createWorkerRequest(sourceTopic, sourcePayloadBytes) {
  const downstreamEnvelope = {
    workspaceId: "workspace-a",
    brokerServer: "broker.meshboard.test",
    topic: sourceTopic,
    payload: bytesToBase64(sourcePayloadBytes),
    receivedAtUtc: "2026-03-14T17:00:00Z"
  };

  return {
    downstreamTopic: "meshboard/workspaces/workspace-a/live/packets",
    payloadBase64: bytesToBase64(new TextEncoder().encode(JSON.stringify(downstreamEnvelope))),
    receivedAtUtc: "2026-03-14T17:00:00Z"
  };
}

async function createEncryptedMeshPacket({ fromNodeNumber, packetId, keyBytes, dataBytes }) {
  const nonce = buildNonce(fromNodeNumber, packetId);
  const cryptoKey = await crypto.subtle.importKey("raw", keyBytes, "AES-CTR", false, ["encrypt"]);
  const cipherBuffer = await crypto.subtle.encrypt(
    {
      name: "AES-CTR",
      counter: nonce,
      length: 128
    },
    cryptoKey,
    dataBytes);

  return encodeMeshPacket({
    fromNodeNumber,
    packetId,
    encryptedBytes: new Uint8Array(cipherBuffer)
  });
}

function encodeMeshPacket({ fromNodeNumber, packetId, encryptedBytes }) {
  return concatBytes(
    encodeTag(1, 0),
    encodeVarint(fromNodeNumber),
    encodeTag(5, 2),
    encodeLengthDelimited(encryptedBytes),
    encodeTag(6, 0),
    encodeVarint(packetId));
}

function createDecodedMeshPacket({ fromNodeNumber, packetId, dataBytes }) {
  return concatBytes(
    encodeTag(1, 0),
    encodeVarint(fromNodeNumber),
    encodeTag(4, 2),
    encodeLengthDelimited(dataBytes),
    encodeTag(6, 0),
    encodeVarint(packetId));
}

function encodeDataMessage({ portNumValue = 1, text = "hello mesh" } = {}) {
  const payload = new TextEncoder().encode(text);

  return concatBytes(
    encodeTag(1, 0),
    encodeVarint(portNumValue),
    encodeTag(2, 2),
    encodeLengthDelimited(payload),
    encodeTag(4, 5),
    encodeFixed32(1234),
    encodeTag(5, 5),
    encodeFixed32(5678));
}

function buildNonce(fromNodeNumber, packetId) {
  const nonce = new Uint8Array(16);
  const view = new DataView(nonce.buffer);
  view.setBigUint64(0, BigInt(packetId), true);
  view.setUint32(8, fromNodeNumber, true);
  return nonce;
}

function encodeTag(fieldNumber, wireType) {
  return encodeVarint((fieldNumber << 3) | wireType);
}

function encodeLengthDelimited(bytes) {
  return concatBytes(encodeVarint(bytes.length), bytes);
}

function encodeFixed32(value) {
  const bytes = new Uint8Array(4);
  new DataView(bytes.buffer).setUint32(0, value, true);
  return bytes;
}

function encodeVarint(value) {
  let current = BigInt(value);
  const bytes = [];

  while (current >= 0x80n) {
    bytes.push(Number((current & 0x7fn) | 0x80n));
    current >>= 7n;
  }

  bytes.push(Number(current));
  return Uint8Array.from(bytes);
}

function concatBytes(...parts) {
  const totalLength = parts.reduce((sum, part) => sum + part.length, 0);
  const combined = new Uint8Array(totalLength);
  let offset = 0;

  for (const part of parts) {
    combined.set(part, offset);
    offset += part.length;
  }

  return combined;
}

function bytesToBase64(bytes) {
  let binary = "";

  for (const value of bytes) {
    binary += String.fromCharCode(value);
  }

  return btoa(binary);
}
