const textDecoder = new TextDecoder();
const decryptResultClassifications = Object.freeze({
  decrypted: "Decrypted",
  encryptedButNotDecrypted: "EncryptedButNotDecrypted",
  notAttempted: "NotAttempted"
});
const failureKinds = Object.freeze({
  decryptFailure: "DecryptFailure",
  malformedPayload: "MalformedPayload",
  noMatchingKey: "NoMatchingKey",
  protobufParseFailure: "ProtobufParseFailure",
  unsupportedPortNum: "UnsupportedPortNum"
});
const portNums = Object.freeze({
  textMessageApp: 1,
  positionApp: 3,
  nodeinfoApp: 4,
  routingApp: 5,
  textMessageCompressedApp: 7,
  waypointApp: 8,
  telemetryApp: 67,
  mapReportApp: 73
});
const packetTypesByPortNum = Object.freeze({
  [portNums.textMessageApp]: {
    name: "TEXT_MESSAGE_APP",
    packetType: "Text Message"
  },
  [portNums.positionApp]: {
    name: "POSITION_APP",
    packetType: "Position Update"
  },
  [portNums.nodeinfoApp]: {
    name: "NODEINFO_APP",
    packetType: "Node Info"
  },
  [portNums.routingApp]: {
    name: "ROUTING_APP",
    packetType: "Routing"
  },
  [portNums.textMessageCompressedApp]: {
    name: "TEXT_MESSAGE_COMPRESSED_APP",
    packetType: "Compressed Text"
  },
  [portNums.waypointApp]: {
    name: "WAYPOINT_APP",
    packetType: "Waypoint"
  },
  [portNums.telemetryApp]: {
    name: "TELEMETRY_APP",
    packetType: "Telemetry"
  },
  [portNums.mapReportApp]: {
    name: "MAP_REPORT_APP",
    packetType: "Map Report"
  }
});

export function getRealtimePacketWorkerConstants() {
  return {
    decryptResultClassifications,
    failureKinds
  };
}

export function normalizeKeyRecords(payload) {
  if (!Array.isArray(payload)) {
    return [];
  }

  return payload
    .filter(record => record && typeof record === "object")
    .map(record => ({
      id: normalizeText(record.id) ?? "",
      name: normalizeText(record.name) ?? "",
      topicPattern: normalizeText(record.topicPattern) ?? "",
      normalizedPattern: normalizeTopicPattern(record.topicPattern),
      brokerServerProfileId: normalizeText(record.brokerServerProfileId),
      normalizedKeyBase64: normalizeText(record.normalizedKeyBase64) ?? "",
      keyLengthBytes: Number.isFinite(record.keyLengthBytes) ? Number(record.keyLengthBytes) : 0
    }))
    .filter(record => record.id && record.normalizedPattern && record.normalizedKeyBase64);
}

export async function processPacketRequest(request, currentKeyRecords) {
  if (!request || typeof request !== "object") {
    return createFailure(
      failureKinds.malformedPayload,
      "The realtime packet request is missing.");
  }

  const downstreamTopic = normalizeText(request.downstreamTopic);
  const receivedAtUtc = normalizeText(request.receivedAtUtc) ?? new Date().toISOString();
  const payloadBase64 = normalizeText(request.payloadBase64);

  if (!payloadBase64) {
    return createFailure(
      failureKinds.malformedPayload,
      "The realtime packet payload is missing.");
  }

  const envelope = parseEnvelope(payloadBase64);
  if (!envelope) {
    return createFailure(
      failureKinds.malformedPayload,
      "The downstream packet envelope is invalid.");
  }

  const workspaceId = normalizeText(envelope.workspaceId);
  const brokerServer = normalizeText(envelope.brokerServer);
  const sourceTopic = normalizeText(envelope.topic);
  const sourcePayloadBase64 = normalizeText(envelope.payload);

  if (!workspaceId || !brokerServer || !sourceTopic || !sourcePayloadBase64) {
    return createFailure(
      failureKinds.malformedPayload,
      "The downstream packet envelope is incomplete.");
  }

  const sourcePayloadBytes = base64ToBytes(sourcePayloadBase64);
  if (!sourcePayloadBytes) {
    return createFailure(
      failureKinds.malformedPayload,
      "The raw packet payload is not valid base64.");
  }

  const rawPacket = createRawPacket({
    workspaceId,
    brokerServer,
    sourceTopic,
    downstreamTopic,
    sourcePayloadBase64,
    sourcePayloadBytes,
    receivedAtUtc: normalizeText(envelope.receivedAtUtc) ?? receivedAtUtc
  });

  const packetMetadata = tryParseMeshtasticPacket(sourcePayloadBytes);
  if (!packetMetadata) {
    return createSuccess(
      decryptResultClassifications.notAttempted,
      rawPacket);
  }

  rawPacket.packetId = packetMetadata.packetId;
  rawPacket.fromNodeNumber = packetMetadata.fromNodeNumber;
  rawPacket.isEncrypted = packetMetadata.payloadVariant === "encrypted";

  if (packetMetadata.payloadVariant === "decoded") {
    return createDecodedPacketResult(
      rawPacket,
      packetMetadata.decodedBytes,
      decryptResultClassifications.notAttempted);
  }

  if (packetMetadata.payloadVariant !== "encrypted") {
    return createSuccess(
      decryptResultClassifications.notAttempted,
      rawPacket);
  }

  if (!packetMetadata.encryptedBytes || packetMetadata.encryptedBytes.length === 0) {
    rawPacket.decryptResultClassification = decryptResultClassifications.encryptedButNotDecrypted;
    rawPacket.failureClassification = failureKinds.malformedPayload;
    return createSuccess(
      decryptResultClassifications.encryptedButNotDecrypted,
      rawPacket,
      failureKinds.malformedPayload,
      "The Meshtastic packet is missing encrypted payload bytes.");
  }

  if (!packetMetadata.fromNodeNumber || !packetMetadata.packetId) {
    rawPacket.decryptResultClassification = decryptResultClassifications.encryptedButNotDecrypted;
    rawPacket.failureClassification = failureKinds.malformedPayload;
    return createSuccess(
      decryptResultClassifications.encryptedButNotDecrypted,
      rawPacket,
      failureKinds.malformedPayload,
      "Encrypted Meshtastic packets require non-zero from and id values.");
  }

  const candidateKeyRecords = selectCandidateKeyRecords(sourceTopic, currentKeyRecords);
  if (candidateKeyRecords.length === 0) {
    rawPacket.decryptResultClassification = decryptResultClassifications.encryptedButNotDecrypted;
    rawPacket.failureClassification = failureKinds.noMatchingKey;
    return createSuccess(
      decryptResultClassifications.encryptedButNotDecrypted,
      rawPacket,
      failureKinds.noMatchingKey,
      "No matching local decryption key is available for this topic.");
  }

  const nonce = buildNonce(packetMetadata.fromNodeNumber, packetMetadata.packetId);
  let sawDecryptFailure = false;
  let sawProtobufParseFailure = false;

  for (const candidate of candidateKeyRecords) {
    const keyBytes = base64ToBytes(candidate.normalizedKeyBase64);

    if (!keyBytes || !isValidAesKeyLength(keyBytes.length)) {
      sawDecryptFailure = true;
      continue;
    }

    let decryptedBytes;

    try {
      decryptedBytes = await decryptPacket(packetMetadata.encryptedBytes, keyBytes, nonce);
    } catch {
      sawDecryptFailure = true;
      continue;
    }

    const decodedPacketOutcome = tryCreateDecodedPacketEvent(decryptedBytes);

    if (!decodedPacketOutcome) {
      sawProtobufParseFailure = true;
      continue;
    }

    rawPacket.decryptionAttempted = true;
    rawPacket.decryptionSucceeded = true;
    rawPacket.decryptedPayloadBase64 = bytesToBase64(decryptedBytes);
    rawPacket.matchedKeyId = candidate.id;
    rawPacket.decryptResultClassification = decryptResultClassifications.decrypted;
    rawPacket.failureClassification = null;

    if (decodedPacketOutcome.kind === "unsupported-port") {
      rawPacket.failureClassification = failureKinds.unsupportedPortNum;
      return createSuccess(
        decryptResultClassifications.decrypted,
        rawPacket,
        failureKinds.unsupportedPortNum,
        `The Meshtastic portnum ${decodedPacketOutcome.portNumValue} is not supported yet.`);
    }

    return createSuccess(
      decryptResultClassifications.decrypted,
      rawPacket,
      null,
      null,
      decodedPacketOutcome.decodedPacket);
  }

  rawPacket.decryptionAttempted = true;
  rawPacket.decryptResultClassification = decryptResultClassifications.encryptedButNotDecrypted;
  rawPacket.failureClassification = sawProtobufParseFailure
    ? failureKinds.protobufParseFailure
    : failureKinds.decryptFailure;

  return createSuccess(
    decryptResultClassifications.encryptedButNotDecrypted,
    rawPacket,
    rawPacket.failureClassification,
    rawPacket.failureClassification === failureKinds.protobufParseFailure
      ? "Decryption completed, but the payload could not be parsed as Meshtastic Data."
      : "All candidate key decrypt attempts failed.");
}

function createRawPacket({
  workspaceId,
  brokerServer,
  sourceTopic,
  downstreamTopic,
  sourcePayloadBase64,
  sourcePayloadBytes,
  receivedAtUtc
}) {
  return {
    workspaceId,
    brokerServer,
    sourceTopic,
    downstreamTopic: downstreamTopic ?? "",
    payloadBase64: sourcePayloadBase64,
    payloadSizeBytes: sourcePayloadBytes.length,
    receivedAtUtc,
    isEncrypted: false,
    decryptionAttempted: false,
    decryptionSucceeded: false,
    decryptResultClassification: decryptResultClassifications.notAttempted,
    failureClassification: null,
    matchedKeyId: null,
    decryptedPayloadBase64: null,
    fromNodeNumber: null,
    packetId: null
  };
}

function createFailure(failureClassification, errorDetail) {
  return {
    isSuccess: false,
    decryptResultClassification: decryptResultClassifications.notAttempted,
    failureClassification,
    errorDetail,
    rawPacket: null,
    decodedPacket: null
  };
}

function createSuccess(
  decryptResultClassification,
  rawPacket,
  failureClassification = null,
  errorDetail = null,
  decodedPacket = null) {
  return {
    isSuccess: true,
    decryptResultClassification,
    failureClassification,
    errorDetail,
    rawPacket,
    decodedPacket
  };
}

function createDecodedPacketResult(rawPacket, decodedBytes, decryptResultClassification) {
  const decodedPacketOutcome = tryCreateDecodedPacketEvent(decodedBytes);

  if (!decodedPacketOutcome) {
    rawPacket.failureClassification = failureKinds.protobufParseFailure;
    return createSuccess(
      decryptResultClassification,
      rawPacket,
      failureKinds.protobufParseFailure,
      "The Meshtastic decoded payload could not be parsed.");
  }

  if (decodedPacketOutcome.kind === "unsupported-port") {
    rawPacket.failureClassification = failureKinds.unsupportedPortNum;
    return createSuccess(
      decryptResultClassification,
      rawPacket,
      failureKinds.unsupportedPortNum,
      `The Meshtastic portnum ${decodedPacketOutcome.portNumValue} is not supported yet.`);
  }

  rawPacket.failureClassification = null;

  return createSuccess(
    decryptResultClassification,
    rawPacket,
    null,
    null,
    decodedPacketOutcome.decodedPacket);
}

async function decryptPacket(cipherTextBytes, keyBytes, counterBytes) {
  const cryptoKey = await crypto.subtle.importKey(
    "raw",
    keyBytes,
    "AES-CTR",
    false,
    ["decrypt"]);

  const plainTextBuffer = await crypto.subtle.decrypt(
    {
      name: "AES-CTR",
      counter: counterBytes,
      length: 128
    },
    cryptoKey,
    cipherTextBytes);

  return new Uint8Array(plainTextBuffer);
}

function selectCandidateKeyRecords(sourceTopic, currentKeyRecords) {
  const normalizedTopic = normalizeTopicPattern(sourceTopic);
  if (!normalizedTopic || !Array.isArray(currentKeyRecords) || currentKeyRecords.length === 0) {
    return [];
  }

  const candidates = currentKeyRecords
    .filter(record => record && typeof record === "object")
    .filter(record => record.normalizedPattern && isPatternMatch(normalizedTopic, record.normalizedPattern))
    .map(record => ({
      ...record,
      matchScore: calculateMatchScore(record.normalizedPattern)
    }))
    .sort((left, right) => {
      if (right.matchScore !== left.matchScore) {
        return right.matchScore - left.matchScore;
      }

      return left.id.localeCompare(right.id);
    });

  const seenKeys = new Set();
  return candidates.filter(candidate => {
    if (seenKeys.has(candidate.normalizedKeyBase64)) {
      return false;
    }

    seenKeys.add(candidate.normalizedKeyBase64);
    return true;
  });
}

function calculateMatchScore(pattern) {
  const segments = pattern.split("/").filter(Boolean);
  let score = 0;

  for (const segment of segments) {
    switch (segment) {
      case "#":
        score += 1;
        break;
      case "+":
        score += 3;
        break;
      default:
        score += 10;
        break;
    }
  }

  return score;
}

function isPatternMatch(topic, pattern) {
  const topicSegments = topic.split("/").filter(Boolean);
  const patternSegments = pattern.split("/").filter(Boolean);

  for (let index = 0; index < patternSegments.length; index += 1) {
    const patternSegment = patternSegments[index];

    if (patternSegment === "#") {
      return true;
    }

    if (index >= topicSegments.length) {
      return false;
    }

    if (patternSegment === "+") {
      continue;
    }

    if (!equalsIgnoreCase(patternSegment, topicSegments[index])) {
      return false;
    }
  }

  return topicSegments.length === patternSegments.length;
}

function normalizeTopicPattern(topicPattern) {
  const normalizedText = normalizeText(topicPattern);
  if (!normalizedText) {
    return "";
  }

  const segments = normalizedText
    .split("/")
    .map(segment => segment.trim())
    .filter(Boolean);

  if (segments.length >= 4 &&
      equalsIgnoreCase(segments[0], "msh") &&
      equalsIgnoreCase(segments[3], "json")) {
    segments[3] = "e";
  }

  return segments.join("/");
}

function tryParseMeshtasticPacket(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return null;
  }

  const servicePacket = tryParseServiceEnvelope(payloadBytes);
  if (servicePacket) {
    return servicePacket;
  }

  return tryParseMeshPacket(payloadBytes);
}

function tryParseServiceEnvelope(payloadBytes) {
  let offset = 0;
  let packetBytes = null;

  while (offset < payloadBytes.length) {
    const tag = readVarint(payloadBytes, offset);
    if (!tag) {
      return null;
    }

    offset = tag.nextOffset;
    const fieldNumber = Number(tag.value >> 3n);
    const wireType = Number(tag.value & 7n);

    if (fieldNumber === 1 && wireType === 2) {
      const fieldBytes = readLengthDelimited(payloadBytes, offset);
      if (!fieldBytes) {
        return null;
      }

      packetBytes = fieldBytes.value;
      offset = fieldBytes.nextOffset;
      continue;
    }

    const nextOffset = skipField(payloadBytes, offset, wireType);
    if (nextOffset < 0) {
      return null;
    }

    offset = nextOffset;
  }

  return packetBytes ? tryParseMeshPacket(packetBytes) : null;
}

function tryParseMeshPacket(payloadBytes) {
  let offset = 0;
  let fromNodeNumber = null;
  let packetId = null;
  let encryptedBytes = null;
  let decodedBytes = null;

  while (offset < payloadBytes.length) {
    const tag = readVarint(payloadBytes, offset);
    if (!tag) {
      return null;
    }

    offset = tag.nextOffset;
    const fieldNumber = Number(tag.value >> 3n);
    const wireType = Number(tag.value & 7n);

    switch (fieldNumber) {
      case 1:
        if (wireType !== 0) {
          return null;
        }

        {
          const field = readVarint(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          fromNodeNumber = numberOrNull(field.value);
        }
        break;
      case 4:
        if (wireType !== 2) {
          return null;
        }

        {
          const field = readLengthDelimited(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          decodedBytes = field.value.length > 0 ? field.value : null;
        }
        break;
      case 5:
        if (wireType !== 2) {
          return null;
        }

        {
          const field = readLengthDelimited(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          encryptedBytes = field.value;
        }
        break;
      case 6:
        if (wireType !== 0) {
          return null;
        }

        {
          const field = readVarint(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          packetId = numberOrNull(field.value);
        }
        break;
      default:
        {
          const nextOffset = skipField(payloadBytes, offset, wireType);
          if (nextOffset < 0) {
            return null;
          }

          offset = nextOffset;
        }
        break;
    }
  }

  if (encryptedBytes && encryptedBytes.length > 0) {
    return {
      payloadVariant: "encrypted",
      fromNodeNumber,
      packetId,
      encryptedBytes,
      decodedBytes: null
    };
  }

  if (decodedBytes) {
    return {
      payloadVariant: "decoded",
      fromNodeNumber,
      packetId,
      encryptedBytes: null,
      decodedBytes
    };
  }

  return {
    payloadVariant: "unknown",
    fromNodeNumber,
    packetId,
    encryptedBytes: null,
    decodedBytes: null
  };
}

function tryCreateDecodedPacketEvent(decodedBytes) {
  const dataMessage = parseDataMessage(decodedBytes);
  if (!dataMessage) {
    return null;
  }

  const portMetadata = getPortMetadata(dataMessage.portNumValue);
  if (!portMetadata) {
    return {
      kind: "unsupported-port",
      portNumValue: dataMessage.portNumValue
    };
  }

  return {
    kind: "supported",
    decodedPacket: {
      portNumValue: dataMessage.portNumValue,
      portNumName: portMetadata.portNumName,
      packetType: portMetadata.packetType,
      payloadBase64: bytesToBase64(dataMessage.payloadBytes),
      payloadSizeBytes: dataMessage.payloadBytes.length,
      payloadPreview: buildPayloadPreview(portMetadata, dataMessage.payloadBytes),
      sourceNodeNumber: dataMessage.sourceNodeNumber,
      destinationNodeNumber: dataMessage.destinationNodeNumber
    }
  };
}

function parseDataMessage(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return null;
  }

  let offset = 0;
  let hasKnownField = false;
  let portNumValue = null;
  let payloadFieldBytes = new Uint8Array();
  let destinationNodeNumber = null;
  let sourceNodeNumber = null;

  while (offset < payloadBytes.length) {
    const tag = readVarint(payloadBytes, offset);
    if (!tag) {
      return null;
    }

    offset = tag.nextOffset;
    const fieldNumber = Number(tag.value >> 3n);
    const wireType = Number(tag.value & 7n);

    switch (fieldNumber) {
      case 1:
        if (wireType !== 0) {
          return null;
        }

        {
          const field = readVarint(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          portNumValue = numberOrNull(field.value);
          hasKnownField = true;
        }
        break;
      case 2:
        if (wireType !== 2) {
          return null;
        }

        {
          const field = readLengthDelimited(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          payloadFieldBytes = field.value;
          hasKnownField = true;
        }
        break;
      case 3:
        if (wireType !== 0) {
          return null;
        }

        {
          const field = readVarint(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          hasKnownField = true;
        }
        break;
      case 4:
        if (wireType !== 5) {
          return null;
        }

        {
          const field = readFixed32(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          destinationNodeNumber = field.value;
          hasKnownField = true;
        }
        break;
      case 5:
        if (wireType !== 5) {
          return null;
        }

        {
          const field = readFixed32(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          sourceNodeNumber = field.value;
          hasKnownField = true;
        }
        break;
      case 6:
      case 7:
        if (wireType !== 5) {
          return null;
        }

        {
          const field = readFixed32(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          hasKnownField = true;
        }
        break;
      default:
        {
          const nextOffset = skipField(payloadBytes, offset, wireType);
          if (nextOffset < 0) {
            return null;
          }

          offset = nextOffset;
        }
        break;
    }
  }

  if (!hasKnownField || portNumValue === null) {
    return null;
  }

  return {
    portNumValue,
    payloadBytes: payloadFieldBytes,
    sourceNodeNumber,
    destinationNodeNumber
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

function buildNonce(fromNodeNumber, packetId) {
  const nonce = new Uint8Array(16);
  const nonceView = new DataView(nonce.buffer);
  nonceView.setBigUint64(0, BigInt(packetId), true);
  nonceView.setUint32(8, fromNodeNumber, true);
  return nonce;
}

function readLengthDelimited(bytes, offset) {
  const lengthField = readVarint(bytes, offset);
  if (!lengthField) {
    return null;
  }

  const length = numberOrNull(lengthField.value);
  if (length === null || length < 0) {
    return null;
  }

  const start = lengthField.nextOffset;
  const end = start + length;

  if (end > bytes.length) {
    return null;
  }

  return {
    value: bytes.slice(start, end),
    nextOffset: end
  };
}

function readFixed32(bytes, offset) {
  if (!(bytes instanceof Uint8Array) || offset < 0 || offset + 4 > bytes.length) {
    return null;
  }

  const view = new DataView(bytes.buffer, bytes.byteOffset + offset, 4);
  return {
    value: view.getUint32(0, true),
    nextOffset: offset + 4
  };
}

function readVarint(bytes, offset) {
  if (!(bytes instanceof Uint8Array) || offset < 0 || offset >= bytes.length) {
    return null;
  }

  let value = 0n;
  let shift = 0n;

  for (let index = offset; index < bytes.length; index += 1) {
    const current = bytes[index];
    value |= BigInt(current & 0x7f) << shift;

    if ((current & 0x80) === 0) {
      return {
        value,
        nextOffset: index + 1
      };
    }

    shift += 7n;
    if (shift > 63n) {
      return null;
    }
  }

  return null;
}

function skipField(bytes, offset, wireType) {
  switch (wireType) {
    case 0:
      {
        const field = readVarint(bytes, offset);
        return field ? field.nextOffset : -1;
      }
    case 1:
      return offset + 8 <= bytes.length ? offset + 8 : -1;
    case 2:
      {
        const field = readLengthDelimited(bytes, offset);
        return field ? field.nextOffset : -1;
      }
    case 5:
      return offset + 4 <= bytes.length ? offset + 4 : -1;
    default:
      return -1;
  }
}

function numberOrNull(value) {
  if (typeof value !== "bigint") {
    return null;
  }

  const numberValue = Number(value);
  return Number.isSafeInteger(numberValue) ? numberValue : null;
}

function isValidAesKeyLength(length) {
  return length === 16 || length === 24 || length === 32;
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

function bytesToBase64(bytes) {
  if (!(bytes instanceof Uint8Array)) {
    return "";
  }

  let binary = "";

  for (const value of bytes) {
    binary += String.fromCharCode(value);
  }

  return btoa(binary);
}

function buildPayloadPreview(portMetadata, payloadBytes) {
  if (portMetadata.portNumValue === portNums.textMessageApp) {
    return decodeTextPayload(payloadBytes);
  }

  return `${portMetadata.packetType} payload (${payloadBytes.length} bytes)`;
}

function decodeTextPayload(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return "Text message payload (0 bytes)";
  }

  try {
    const text = normalizePreviewText(textDecoder.decode(payloadBytes));
    return text.length > 0
      ? text
      : `Text message payload (${payloadBytes.length} bytes)`;
  } catch {
    return `Text message payload (${payloadBytes.length} bytes)`;
  }
}

function normalizePreviewText(value) {
  if (typeof value !== "string") {
    return "";
  }

  const normalized = value
    .replace(/\s+/g, " ")
    .trim();

  return normalized.length <= 160
    ? normalized
    : `${normalized.slice(0, 160)}...`;
}

function getPortMetadata(portNumValue) {
  const metadata = packetTypesByPortNum[portNumValue];
  return metadata
    ? {
        portNumValue,
        portNumName: metadata.name,
        packetType: metadata.packetType
      }
    : null;
}

function equalsIgnoreCase(left, right) {
  return typeof left === "string" &&
    typeof right === "string" &&
    left.localeCompare(right, undefined, { sensitivity: "accent" }) === 0;
}

function normalizeText(value) {
  return typeof value === "string" && value.trim().length > 0
    ? value.trim()
    : null;
}
