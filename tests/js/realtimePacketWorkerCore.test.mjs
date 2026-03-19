import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";

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

test("processPacketRequest emits node projection metadata for node info payloads", async () => {
  const packetBytes = createDecodedMeshPacket({
    fromNodeNumber: 0x00001234,
    packetId: 92,
    dataBytes: encodeDataMessage({
      portNumValue: 4,
      sourceNodeNumber: 0x00001234,
      payloadBytes: encodeNodeInfoPayload({
        nodeId: "!00001234",
        longName: "Field Relay",
        shortName: "RELAY"
      })
    })
  });

  const result = await processPacketRequest(
    createWorkerRequest("msh/US/2/e/LongFast/!abcd1234", packetBytes),
    normalizeKeyRecords([]));

  assert.equal(result.isSuccess, true);
  assert.equal(result.decodedPacket?.packetType, "Node Info");
  assert.equal(result.decodedPacket?.payloadPreview, "Node info: Field Relay (RELAY)");
  assert.equal(result.decodedPacket?.nodeProjection?.nodeId, "!00001234");
  assert.equal(result.decodedPacket?.nodeProjection?.nodeNumber, 0x00001234);
  assert.equal(result.decodedPacket?.nodeProjection?.shortName, "RELAY");
  assert.equal(result.decodedPacket?.nodeProjection?.longName, "Field Relay");
  assert.equal(result.decodedPacket?.nodeProjection?.lastHeardChannel, "US/LongFast");
  assert.equal(result.decodedPacket?.nodeProjection?.packetType, "Node Info");
  assert.equal(result.decodedPacket?.nodeProjection?.payloadPreview, "Node info: Field Relay (RELAY)");
});

test("processPacketRequest emits node projection metrics for position and telemetry payloads", async () => {
  const positionPacketBytes = createDecodedMeshPacket({
    fromNodeNumber: 0x00005678,
    packetId: 93,
    dataBytes: encodeDataMessage({
      portNumValue: 3,
      sourceNodeNumber: 0x00005678,
      payloadBytes: encodePositionPayload({
        latitude: 48.85661,
        longitude: 2.35222
      })
    })
  });

  const telemetryPacketBytes = createDecodedMeshPacket({
    fromNodeNumber: 0x00005678,
    packetId: 94,
    dataBytes: encodeDataMessage({
      portNumValue: 67,
      sourceNodeNumber: 0x00005678,
      payloadBytes: encodeTelemetryPayload({
        batteryLevelPercent: 89,
        voltage: 4.12,
        channelUtilization: 11.5,
        airUtilTx: 2.7,
        uptimeSeconds: 7200,
        temperatureCelsius: 21.5,
        relativeHumidity: 48.2,
        barometricPressure: 1013.6
      })
    })
  });

  const positionResult = await processPacketRequest(
    createWorkerRequest("msh/EU_868/2/e/LongFast/!abcd1234", positionPacketBytes),
    normalizeKeyRecords([]));
  const telemetryResult = await processPacketRequest(
    createWorkerRequest("msh/EU_868/2/e/LongFast/!abcd1234", telemetryPacketBytes),
    normalizeKeyRecords([]));

  assert.equal(positionResult.decodedPacket?.nodeProjection?.nodeId, "!00005678");
  assert.equal(positionResult.decodedPacket?.nodeProjection?.nodeNumber, 0x00005678);
  assert.equal(positionResult.decodedPacket?.nodeProjection?.lastKnownLatitude, 48.85661);
  assert.equal(positionResult.decodedPacket?.nodeProjection?.lastKnownLongitude, 2.35222);
  assert.equal(positionResult.decodedPacket?.payloadPreview, "Position: 48.85661, 2.35222");
  assert.equal(positionResult.decodedPacket?.nodeProjection?.payloadPreview, "Position: 48.85661, 2.35222");

  assert.equal(telemetryResult.decodedPacket?.nodeProjection?.batteryLevelPercent, 89);
  assert.ok(Math.abs((telemetryResult.decodedPacket?.nodeProjection?.voltage ?? 0) - 4.12) < 0.0001);
  assert.ok(Math.abs((telemetryResult.decodedPacket?.nodeProjection?.channelUtilization ?? 0) - 11.5) < 0.0001);
  assert.ok(Math.abs((telemetryResult.decodedPacket?.nodeProjection?.airUtilTx ?? 0) - 2.7) < 0.0001);
  assert.equal(telemetryResult.decodedPacket?.nodeProjection?.uptimeSeconds, 7200);
  assert.ok(Math.abs((telemetryResult.decodedPacket?.nodeProjection?.temperatureCelsius ?? 0) - 21.5) < 0.0001);
  assert.ok(Math.abs((telemetryResult.decodedPacket?.nodeProjection?.relativeHumidity ?? 0) - 48.2) < 0.0001);
  assert.ok(Math.abs((telemetryResult.decodedPacket?.nodeProjection?.barometricPressure ?? 0) - 1013.6) < 0.0001);
  assert.match(telemetryResult.decodedPacket?.payloadPreview ?? "", /^Device metrics:/);
  assert.match(telemetryResult.decodedPacket?.nodeProjection?.payloadPreview ?? "", /^Device metrics:/);
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

test("processPacketRequest decodes captured public mqtt envelopes with the normalized default key", async () => {
  const fixtureLines = fs.readFileSync(
    new URL("./fixtures/meshtastic_public_us_messages_hex.txt", import.meta.url),
    "utf8")
    .trim()
    .split("\n")
    .filter(Boolean);

  const keyRecords = normalizeKeyRecords([
    {
      id: "public-default",
      name: "Public default",
      topicPattern: "msh/US/2/e/#",
      normalizedKeyBase64: "1PG7OiApB1nwvP+rz05pAQ==",
      keyLengthBytes: 16
    }
  ]);

  let decodedCount = 0;
  let nodeInfoCount = 0;
  let positionCount = 0;

  for (const line of fixtureLines) {
    const separatorIndex = line.indexOf(" ");
    const topic = line.slice(0, separatorIndex);
    const payloadHex = line.slice(separatorIndex + 1).trim();

    const result = await processPacketRequest(
      createWorkerRequest(topic, Buffer.from(payloadHex, "hex")),
      keyRecords);

    if (!result.decodedPacket) {
      continue;
    }

    decodedCount += 1;

    if (result.decodedPacket.packetType === "Node Info") {
      nodeInfoCount += 1;
    }

    if (result.decodedPacket.packetType === "Position Update") {
      positionCount += 1;
    }
  }

  assert.equal(decodedCount, 4);
  assert.equal(nodeInfoCount, 1);
  assert.equal(positionCount, 3);
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
    encodeTag(1, 5),
    encodeFixed32(fromNodeNumber),
    encodeTag(5, 2),
    encodeLengthDelimited(encryptedBytes),
    encodeTag(6, 5),
    encodeFixed32(packetId));
}

function createDecodedMeshPacket({ fromNodeNumber, packetId, dataBytes }) {
  return concatBytes(
    encodeTag(1, 5),
    encodeFixed32(fromNodeNumber),
    encodeTag(4, 2),
    encodeLengthDelimited(dataBytes),
    encodeTag(6, 5),
    encodeFixed32(packetId));
}

function encodeDataMessage({
  portNumValue = 1,
  text = "hello mesh",
  payloadBytes = null,
  destinationNodeNumber = 1234,
  sourceNodeNumber = 5678
} = {}) {
  const payload = payloadBytes ?? new TextEncoder().encode(text);

  return concatBytes(
    encodeTag(1, 0),
    encodeVarint(portNumValue),
    encodeTag(2, 2),
    encodeLengthDelimited(payload),
    encodeTag(4, 5),
    encodeFixed32(destinationNodeNumber),
    encodeTag(5, 5),
    encodeFixed32(sourceNodeNumber));
}

function encodeNodeInfoPayload({ nodeId, longName, shortName }) {
  return concatBytes(
    encodeTag(1, 2),
    encodeLengthDelimited(new TextEncoder().encode(nodeId)),
    encodeTag(2, 2),
    encodeLengthDelimited(new TextEncoder().encode(longName)),
    encodeTag(3, 2),
    encodeLengthDelimited(new TextEncoder().encode(shortName)));
}

function encodePositionPayload({ latitude, longitude }) {
  return concatBytes(
    encodeTag(1, 5),
    encodeSFixed32(Math.round(latitude * 10000000)),
    encodeTag(2, 5),
    encodeSFixed32(Math.round(longitude * 10000000)));
}

function encodeTelemetryPayload({
  batteryLevelPercent,
  voltage,
  channelUtilization,
  airUtilTx,
  uptimeSeconds,
  temperatureCelsius,
  relativeHumidity,
  barometricPressure
}) {
  const deviceMetrics = concatBytes(
    encodeTag(1, 0),
    encodeVarint(batteryLevelPercent),
    encodeTag(2, 5),
    encodeFloat32(voltage),
    encodeTag(3, 5),
    encodeFloat32(channelUtilization),
    encodeTag(4, 5),
    encodeFloat32(airUtilTx),
    encodeTag(12, 0),
    encodeVarint(uptimeSeconds));
  const environmentMetrics = concatBytes(
    encodeTag(1, 5),
    encodeFloat32(temperatureCelsius),
    encodeTag(2, 5),
    encodeFloat32(relativeHumidity),
    encodeTag(5, 5),
    encodeFloat32(barometricPressure));

  return concatBytes(
    encodeTag(2, 2),
    encodeLengthDelimited(deviceMetrics),
    encodeTag(8, 2),
    encodeLengthDelimited(environmentMetrics));
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

function encodeSFixed32(value) {
  const bytes = new Uint8Array(4);
  new DataView(bytes.buffer).setInt32(0, value, true);
  return bytes;
}

function encodeFloat32(value) {
  const bytes = new Uint8Array(4);
  new DataView(bytes.buffer).setFloat32(0, value, true);
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
