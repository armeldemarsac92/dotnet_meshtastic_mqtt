const textDecoder = new TextDecoder();
const textEncoder = new TextEncoder();
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
  tracerouteApp: 70,
  neighborinfoApp: 71,
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
  [portNums.tracerouteApp]: {
    name: "TRACEROUTE_APP",
    packetType: "Traceroute"
  },
  [portNums.neighborinfoApp]: {
    name: "NEIGHBORINFO_APP",
    packetType: "Neighbor Info"
  },
  [portNums.mapReportApp]: {
    name: "MAP_REPORT_APP",
    packetType: "Map Report"
  }
});
const routingErrorNamesByCode = Object.freeze({
  0: "NONE",
  1: "NO_ROUTE",
  2: "GOT_NAK",
  3: "TIMEOUT",
  4: "NO_INTERFACE",
  5: "MAX_RETRANSMIT",
  6: "NO_CHANNEL",
  7: "TOO_LARGE",
  8: "NO_RESPONSE",
  9: "DUTY_CYCLE_LIMIT",
  32: "BAD_REQUEST",
  33: "NOT_AUTHORIZED",
  34: "PKI_FAILED",
  35: "PKI_UNKNOWN_PUBKEY",
  36: "ADMIN_BAD_SESSION_KEY",
  37: "ADMIN_PUBLIC_KEY_UNAUTHORIZED",
  38: "RATE_LIMIT_EXCEEDED",
  39: "PKI_SEND_FAIL_PUBLIC_KEY"
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
    const jsonPacketOutcome = tryCreateJsonDecodedPacketEvent(rawPacket, sourcePayloadBytes);
    if (!jsonPacketOutcome) {
      return createSuccess(
        decryptResultClassifications.notAttempted,
        rawPacket);
    }

    if (jsonPacketOutcome.kind === "unsupported-port") {
      rawPacket.failureClassification = failureKinds.unsupportedPortNum;
      return createSuccess(
        decryptResultClassifications.notAttempted,
        rawPacket,
        failureKinds.unsupportedPortNum,
        `The Meshtastic portnum ${jsonPacketOutcome.portNumValue} is not supported yet.`);
    }

    return createSuccess(
      decryptResultClassifications.notAttempted,
      rawPacket,
      null,
      null,
      jsonPacketOutcome.decodedPacket);
  }

  rawPacket.packetId = packetMetadata.packetId;
  rawPacket.fromNodeNumber = packetMetadata.fromNodeNumber;
  rawPacket.isEncrypted = packetMetadata.payloadVariant === "encrypted";
  rawPacket.rxSnr = packetMetadata.rxSnr ?? null;
  rawPacket.rxRssi = packetMetadata.rxRssi ?? null;
  rawPacket.hopLimit = packetMetadata.hopLimit ?? null;
  rawPacket.hopStart = packetMetadata.hopStart ?? null;
  rawPacket.gatewayNodeId = packetMetadata.gatewayNodeId ?? null;

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

    const decodedPacketOutcome = tryCreateDecodedPacketEvent(rawPacket, decryptedBytes);

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
  const decodedPacketOutcome = tryCreateDecodedPacketEvent(rawPacket, decodedBytes);

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
  let gatewayNodeId = null;

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

    if (fieldNumber === 3 && wireType === 2) {
      const fieldBytes = readLengthDelimited(payloadBytes, offset);
      if (!fieldBytes) {
        return null;
      }

      const decoded = textDecoder.decode(fieldBytes.value);
      if (decoded && decoded.trim().length > 0) {
        gatewayNodeId = decoded.trim();
      }

      offset = fieldBytes.nextOffset;
      continue;
    }

    const nextOffset = skipField(payloadBytes, offset, wireType);
    if (nextOffset < 0) {
      return null;
    }

    offset = nextOffset;
  }

  if (!packetBytes) {
    return null;
  }

  const packet = tryParseMeshPacket(packetBytes);
  if (!packet) {
    return null;
  }

  if (gatewayNodeId) {
    packet.gatewayNodeId = gatewayNodeId;
  }

  return packet;
}

function tryParseMeshPacket(payloadBytes) {
  let offset = 0;
  let fromNodeNumber = null;
  let packetId = null;
  let encryptedBytes = null;
  let decodedBytes = null;
  let rxSnr = null;
  let hopLimit = null;
  let rxRssi = null;
  let hopStart = null;

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
        {
          const field = readMeshPacketUint32(payloadBytes, offset, wireType);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          fromNodeNumber = field.value;
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
        {
          const field = readMeshPacketUint32(payloadBytes, offset, wireType);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          packetId = field.value;
        }
        break;
      case 8:
        if (wireType === 5) {
          const field = readFloat32(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          rxSnr = field.value !== 0 && isFinite(field.value) ? field.value : null;
        } else {
          const nextOffset = skipField(payloadBytes, offset, wireType);
          if (nextOffset < 0) {
            return null;
          }

          offset = nextOffset;
        }
        break;
      case 9:
        {
          const field = readVarint(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          hopLimit = numberOrNull(field.value);
        }
        break;
      case 12:
        {
          const field = readVarint(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          rxRssi = int32OrNull(field.value);
          if (rxRssi === 0) {
            rxRssi = null;
          }
        }
        break;
      case 15:
        {
          const field = readVarint(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          hopStart = numberOrNull(field.value);
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

  const signalQuality = { rxSnr, rxRssi, hopLimit, hopStart, gatewayNodeId: null };

  if (encryptedBytes && encryptedBytes.length > 0) {
    return {
      payloadVariant: "encrypted",
      fromNodeNumber,
      packetId,
      encryptedBytes,
      decodedBytes: null,
      ...signalQuality
    };
  }

  if (decodedBytes) {
    return {
      payloadVariant: "decoded",
      fromNodeNumber,
      packetId,
      encryptedBytes: null,
      decodedBytes,
      ...signalQuality
    };
  }

  return {
    payloadVariant: "unknown",
    fromNodeNumber,
    packetId,
    encryptedBytes: null,
    decodedBytes: null,
    ...signalQuality
  };
}

function readMeshPacketUint32(bytes, offset, wireType) {
  switch (wireType) {
    case 0:
      {
        const field = readVarint(bytes, offset);
        if (!field) {
          return null;
        }

        const numericValue = numberOrNull(field.value);
        if (numericValue === null) {
          return null;
        }

        return {
          value: numericValue,
          nextOffset: field.nextOffset
        };
      }
    case 5:
      return readFixed32(bytes, offset);
    default:
      return null;
  }
}

function tryCreateDecodedPacketEvent(rawPacket, decodedBytes) {
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

  const nodeProjectionOutcome = tryCreateNodeProjectionEvent(rawPacket, dataMessage, portMetadata);
  const neighborInfoOutcome = tryCreateNeighborInfoEvent(rawPacket, dataMessage);
  const routingInfoOutcome = tryCreateRoutingInfoEvent(dataMessage);
  const fromNodeId = rawPacket.fromNodeNumber ? `!${rawPacket.fromNodeNumber.toString(16).padStart(8, "0")}` : null;
  const toNodeId = dataMessage.destinationNodeNumber ? `!${dataMessage.destinationNodeNumber.toString(16).padStart(8, "0")}` : null;
  const tracerouteInfoOutcome = tryCreateTracerouteInfoEvent(dataMessage, fromNodeId, toNodeId);
  const payloadPreview = neighborInfoOutcome?.payloadPreview ??
    routingInfoOutcome?.payloadPreview ??
    tracerouteInfoOutcome?.payloadPreview ??
    nodeProjectionOutcome?.payloadPreview ??
    buildPayloadPreview(portMetadata, dataMessage.payloadBytes);

  return {
    kind: "supported",
    decodedPacket: {
      portNumValue: dataMessage.portNumValue,
      portNumName: portMetadata.portNumName,
      packetType: portMetadata.packetType,
      payloadBase64: bytesToBase64(dataMessage.payloadBytes),
      payloadSizeBytes: dataMessage.payloadBytes.length,
      payloadPreview,
      sourceNodeNumber: dataMessage.sourceNodeNumber,
      destinationNodeNumber: dataMessage.destinationNodeNumber,
      nodeProjection: nodeProjectionOutcome?.nodeProjection ?? null,
      neighborInfo: neighborInfoOutcome?.neighborInfo ?? null,
      routingInfo: routingInfoOutcome?.routingInfo ?? null,
      tracerouteInfo: tracerouteInfoOutcome?.tracerouteInfo ?? null
    }
  };
}

function tryCreateJsonDecodedPacketEvent(rawPacket, payloadBytes) {
  const root = tryParseJsonSourcePayload(payloadBytes);
  if (!root) {
    return null;
  }

  const portNumValue = resolveJsonPortNumValue(root);
  if (portNumValue === null) {
    return null;
  }

  const portMetadata = getPortMetadata(portNumValue);
  if (!portMetadata) {
    return {
      kind: "unsupported-port",
      portNumValue
    };
  }

  const payloadContext = resolveJsonPayloadContext(root, portNumValue);
  const payloadValue = payloadContext.payloadValue;
  const payloadBytesForEvent = payloadContext.bytes;
  const sourceNodeNumber = resolveJsonNodeNumber(root, "source", "from");
  const destinationNodeNumber = resolveJsonNodeNumber(root, "dest", "to");
  const nodeProjectionOutcome = tryCreateJsonNodeProjectionEvent(
    rawPacket,
    portNumValue,
    payloadBytesForEvent,
    payloadContext.isBinaryPayload,
    payloadValue,
    sourceNodeNumber,
    portMetadata);
  const neighborInfoOutcome = tryCreateJsonNeighborInfoEvent(
    rawPacket,
    portNumValue,
    payloadBytesForEvent,
    payloadContext.isBinaryPayload,
    payloadValue,
    sourceNodeNumber);
  const routingInfoOutcome = tryCreateJsonRoutingInfoEvent(
    portNumValue,
    payloadBytesForEvent,
    payloadContext.isBinaryPayload,
    payloadValue);
  const jsonFromNodeId = sourceNodeNumber ? `!${sourceNodeNumber.toString(16).padStart(8, "0")}` : null;
  const jsonToNodeId = destinationNodeNumber ? `!${destinationNodeNumber.toString(16).padStart(8, "0")}` : null;
  const tracerouteInfoOutcome = tryCreateJsonTracerouteInfoEvent(
    portNumValue,
    payloadBytesForEvent,
    payloadContext.isBinaryPayload,
    payloadValue,
    jsonFromNodeId,
    jsonToNodeId);
  const payloadPreview = neighborInfoOutcome?.payloadPreview ??
    routingInfoOutcome?.payloadPreview ??
    tracerouteInfoOutcome?.payloadPreview ??
    nodeProjectionOutcome?.payloadPreview ??
    buildJsonDecodedPayloadPreview(
      portNumValue,
      payloadValue,
      payloadBytesForEvent,
      payloadContext.isBinaryPayload,
      root);

  return {
    kind: "supported",
    decodedPacket: {
      portNumValue,
      portNumName: portMetadata.portNumName,
      packetType: portMetadata.packetType,
      payloadBase64: bytesToBase64(payloadBytesForEvent),
      payloadSizeBytes: payloadBytesForEvent.length,
      payloadPreview,
      sourceNodeNumber,
      destinationNodeNumber,
      nodeProjection: nodeProjectionOutcome?.nodeProjection ?? null,
      neighborInfo: neighborInfoOutcome?.neighborInfo ?? null,
      routingInfo: routingInfoOutcome?.routingInfo ?? null,
      tracerouteInfo: tracerouteInfoOutcome?.tracerouteInfo ?? null
    }
  };
}

function tryCreateJsonNodeProjectionEvent(
  rawPacket,
  portNumValue,
  payloadBytes,
  isBinaryPayload,
  payloadValue,
  sourceNodeNumber,
  portMetadata) {
  if (!rawPacket || typeof rawPacket !== "object") {
    return null;
  }

  const nodeId = normalizeNodeIdFromNumber(sourceNodeNumber) ??
    resolveJsonNodeId(
      payloadValue,
      "nodeId",
      "node_id",
      "id") ??
    resolveJsonNodeId(
      rawPacket,
      "sourceNodeId",
      "fromNodeId") ??
    resolveJsonNodeIdFromRoot(payloadValue) ??
    resolveJsonNodeIdFromTopic(rawPacket.sourceTopic);

  if (!nodeId) {
    return null;
  }

  const lastHeardAtUtc = normalizeText(rawPacket.receivedAtUtc) ?? new Date().toISOString();
  const projection = {
    nodeId,
    nodeNumber: sourceNodeNumber,
    lastHeardAtUtc,
    lastHeardChannel: tryResolveChannelKeyFromTopic(rawPacket.sourceTopic),
    lastTextMessageAtUtc: portNumValue === portNums.textMessageApp
      ? lastHeardAtUtc
      : null,
    packetType: portMetadata.packetType,
    payloadPreview: null,
    shortName: null,
    longName: null,
    lastKnownLatitude: null,
    lastKnownLongitude: null,
    batteryLevelPercent: null,
    voltage: null,
    channelUtilization: null,
    airUtilTx: null,
    uptimeSeconds: null,
    temperatureCelsius: null,
    relativeHumidity: null,
    barometricPressure: null
  };

  let payloadPreview = buildJsonDecodedPayloadPreview(portNumValue, payloadValue, payloadBytes, isBinaryPayload, {
    payload: payloadValue
  });

  switch (portNumValue) {
    case portNums.textMessageApp:
      payloadPreview = decodeJsonTextPayload(payloadValue, payloadBytes);
      break;
    case portNums.nodeinfoApp:
      {
        const nodeInfo = isBinaryPayload && payloadBytes.length > 0
          ? parseNodeInfoPayload(payloadBytes)
          : normalizeJsonNodeInfoPayload(payloadValue, sourceNodeNumber);
        if (nodeInfo) {
          projection.nodeId = nodeInfo.nodeId ?? projection.nodeId;
          projection.shortName = nodeInfo.shortName;
          projection.longName = nodeInfo.longName;
          payloadPreview = buildNodeInfoPreview(nodeInfo);
        }
      }
      break;
    case portNums.positionApp:
      {
        const position = isBinaryPayload && payloadBytes.length > 0
          ? parsePositionPayload(payloadBytes)
          : normalizeJsonPositionPayload(payloadValue);
        if (position) {
          projection.lastKnownLatitude = position.latitude;
          projection.lastKnownLongitude = position.longitude;
          payloadPreview = buildPositionPreview(position);
        }
      }
      break;
    case portNums.telemetryApp:
      {
        const telemetry = isBinaryPayload && payloadBytes.length > 0
          ? parseTelemetryPayload(payloadBytes)
          : normalizeJsonTelemetryPayload(payloadValue);
        if (telemetry) {
          projection.batteryLevelPercent = telemetry.batteryLevelPercent;
          projection.voltage = telemetry.voltage;
          projection.channelUtilization = telemetry.channelUtilization;
          projection.airUtilTx = telemetry.airUtilTx;
          projection.uptimeSeconds = telemetry.uptimeSeconds;
          projection.temperatureCelsius = telemetry.temperatureCelsius;
          projection.relativeHumidity = telemetry.relativeHumidity;
          projection.barometricPressure = telemetry.barometricPressure;
          payloadPreview = telemetry.payloadPreview;
        }
      }
      break;
    case portNums.neighborinfoApp:
      {
        const neighborInfo = isBinaryPayload && payloadBytes.length > 0
          ? parseNeighborInfoPayload(payloadBytes, sourceNodeNumber)
          : normalizeJsonNeighborInfoPayload(payloadValue, sourceNodeNumber);
        if (neighborInfo) {
          projection.nodeId = neighborInfo.reportingNodeId ?? projection.nodeId;
          payloadPreview = buildNeighborInfoPreview(neighborInfo);
        }
      }
      break;
  }

  projection.payloadPreview = payloadPreview;

  return {
    nodeProjection: projection,
    payloadPreview
  };
}

function tryCreateJsonNeighborInfoEvent(
  rawPacket,
  portNumValue,
  payloadBytes,
  isBinaryPayload,
  payloadValue,
  sourceNodeNumber) {
  if (!rawPacket || typeof rawPacket !== "object" || portNumValue !== portNums.neighborinfoApp) {
    return null;
  }

  const neighborInfo = isBinaryPayload && payloadBytes.length > 0
    ? parseNeighborInfoPayload(payloadBytes, sourceNodeNumber)
    : normalizeJsonNeighborInfoPayload(payloadValue, sourceNodeNumber);

  if (!neighborInfo?.reportingNodeId) {
    return null;
  }

  return {
    neighborInfo: {
      reportingNodeId: neighborInfo.reportingNodeId,
      neighbors: neighborInfo.neighbors.map((neighbor) => ({
        nodeId: neighbor.nodeId,
        snrDb: neighbor.snrDb,
        lastRxAtUtc: neighbor.lastRxAtUtc
      }))
    },
    payloadPreview: buildNeighborInfoPreview(neighborInfo)
  };
}

function tryCreateRoutingInfoEvent(dataMessage) {
  if (!dataMessage || typeof dataMessage !== "object" || dataMessage.portNumValue !== portNums.routingApp) {
    return null;
  }

  const routingInfo = parseRoutingPayload(dataMessage.payloadBytes);

  if (!routingInfo) {
    return null;
  }

  return {
    routingInfo: {
      kind: routingInfo.kind,
      errorCode: routingInfo.errorCode,
      errorName: routingInfo.errorName,
      routeNodeIds: routingInfo.routeNodeIds,
      snrTowards: routingInfo.snrTowards,
      routeBackNodeIds: routingInfo.routeBackNodeIds,
      snrBack: routingInfo.snrBack
    },
    payloadPreview: buildRoutingPreview(routingInfo)
  };
}

function tryCreateJsonRoutingInfoEvent(portNumValue, payloadBytes, isBinaryPayload, payloadValue) {
  if (portNumValue !== portNums.routingApp) {
    return null;
  }

  const routingInfo = isBinaryPayload && payloadBytes.length > 0
    ? parseRoutingPayload(payloadBytes)
    : normalizeJsonRoutingPayload(payloadValue);

  if (!routingInfo) {
    return null;
  }

  return {
    routingInfo: {
      kind: routingInfo.kind,
      errorCode: routingInfo.errorCode,
      errorName: routingInfo.errorName,
      routeNodeIds: routingInfo.routeNodeIds,
      snrTowards: routingInfo.snrTowards,
      routeBackNodeIds: routingInfo.routeBackNodeIds,
      snrBack: routingInfo.snrBack
    },
    payloadPreview: buildRoutingPreview(routingInfo)
  };
}

function buildTraceroutePreview(routeDiscovery, fromNodeId, toNodeId) {
  const allNodes = [];

  if (fromNodeId) {
    allNodes.push(fromNodeId);
  }

  allNodes.push(...routeDiscovery.routeNodeIds);

  if (toNodeId) {
    allNodes.push(toNodeId);
  }

  if (allNodes.length < 2) {
    return "Traceroute";
  }

  const hopCount = routeDiscovery.routeNodeIds.length;
  const suffix = hopCount === 1 ? "hop" : "hops";
  return `Traceroute: ${allNodes.join(" -> ")} (${hopCount} ${suffix})`;
}

function tryCreateTracerouteInfoEvent(dataMessage, fromNodeId, toNodeId) {
  if (!dataMessage || typeof dataMessage !== "object" || dataMessage.portNumValue !== portNums.tracerouteApp) {
    return null;
  }

  const routeDiscovery = parseRouteDiscoveryPayload(dataMessage.payloadBytes);

  if (!routeDiscovery) {
    return null;
  }

  return {
    tracerouteInfo: {
      kind: "traceroute",
      errorCode: null,
      errorName: null,
      routeNodeIds: routeDiscovery.routeNodeIds,
      snrTowards: routeDiscovery.snrTowards,
      routeBackNodeIds: routeDiscovery.routeBackNodeIds,
      snrBack: routeDiscovery.snrBack
    },
    payloadPreview: buildTraceroutePreview(routeDiscovery, fromNodeId, toNodeId)
  };
}

function tryCreateJsonTracerouteInfoEvent(portNumValue, payloadBytes, isBinaryPayload, payloadValue, fromNodeId, toNodeId) {
  if (portNumValue !== portNums.tracerouteApp) {
    return null;
  }

  const routeDiscovery = isBinaryPayload && payloadBytes.length > 0
    ? parseRouteDiscoveryPayload(payloadBytes)
    : null;

  if (!routeDiscovery) {
    return null;
  }

  return {
    tracerouteInfo: {
      kind: "traceroute",
      errorCode: null,
      errorName: null,
      routeNodeIds: routeDiscovery.routeNodeIds,
      snrTowards: routeDiscovery.snrTowards,
      routeBackNodeIds: routeDiscovery.routeBackNodeIds,
      snrBack: routeDiscovery.snrBack
    },
    payloadPreview: buildTraceroutePreview(routeDiscovery, fromNodeId, toNodeId)
  };
}

function tryParseJsonSourcePayload(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return null;
  }

  try {
    const parsed = JSON.parse(textDecoder.decode(payloadBytes));
    return parsed && typeof parsed === "object" && !Array.isArray(parsed)
      ? parsed
      : null;
  } catch {
    return null;
  }
}

function resolveJsonPortNumValue(root) {
  const decoded = getJsonProperty(root, "decoded");
  const explicitPortNum = parseJsonPortNumValue(decoded && typeof decoded === "object"
    ? getJsonProperty(decoded, "portnum", "portNum")
    : undefined);

  if (explicitPortNum !== null) {
    return explicitPortNum;
  }

  return mapJsonTypeToPortNum(getJsonString(root, "type", "packetType", "packet_type"));
}

function resolveJsonPayloadContext(root, portNumValue) {
  const decoded = getJsonProperty(root, "decoded");
  const decodedPayloadValue = decoded && typeof decoded === "object"
    ? getJsonProperty(decoded, "payload")
    : undefined;
  const directPayloadValue = getJsonProperty(root, "payload");
  const textValue = decodeJsonTextValue(decodedPayloadValue) ??
    decodeJsonTextValue(directPayloadValue) ??
    getJsonString(root, "text", "message");

  if (typeof decodedPayloadValue === "string") {
    const decodedPayloadBytes = tryDecodeBase64Strict(decodedPayloadValue);
    if (decodedPayloadBytes) {
      return {
        payloadValue: directPayloadValue,
        bytes: decodedPayloadBytes,
        isBinaryPayload: true
      };
    }
  }

  if (portNumValue === portNums.textMessageApp && textValue) {
    return {
      payloadValue: directPayloadValue,
      bytes: textEncoder.encode(textValue),
      isBinaryPayload: false
    };
  }

  if (directPayloadValue && typeof directPayloadValue === "object") {
    return {
      payloadValue: directPayloadValue,
      bytes: textEncoder.encode(JSON.stringify(directPayloadValue)),
      isBinaryPayload: false
    };
  }

  if (typeof directPayloadValue === "string") {
    const directPayloadBytes = tryDecodeBase64Strict(directPayloadValue);
    if (directPayloadBytes) {
      return {
        payloadValue: directPayloadValue,
        bytes: directPayloadBytes,
        isBinaryPayload: true
      };
    }

    return {
      payloadValue: directPayloadValue,
      bytes: textEncoder.encode(directPayloadValue),
      isBinaryPayload: false
    };
  }

  return {
    payloadValue: directPayloadValue,
    bytes: textEncoder.encode(JSON.stringify(root)),
    isBinaryPayload: false
  };
}

function buildJsonDecodedPayloadPreview(portNumValue, payloadValue, payloadBytes, isBinaryPayload, root) {
  switch (portNumValue) {
    case portNums.textMessageApp:
      return decodeJsonTextPayload(payloadValue ?? getJsonProperty(root, "payload"), payloadBytes);
    case portNums.nodeinfoApp:
      {
        const nodeInfo = isBinaryPayload && payloadBytes.length > 0
          ? parseNodeInfoPayload(payloadBytes)
          : normalizeJsonNodeInfoPayload(payloadValue, resolveJsonNodeNumber(root, "from", "source"));
        return buildNodeInfoPreview(nodeInfo);
      }
    case portNums.positionApp:
      {
        const position = isBinaryPayload && payloadBytes.length > 0
          ? parsePositionPayload(payloadBytes)
          : normalizeJsonPositionPayload(payloadValue);
        return buildPositionPreview(position);
      }
    case portNums.routingApp:
      {
        const routingInfo = isBinaryPayload && payloadBytes.length > 0
          ? parseRoutingPayload(payloadBytes)
          : normalizeJsonRoutingPayload(payloadValue);
        return buildRoutingPreview(routingInfo);
      }
    case portNums.telemetryApp:
      {
        const telemetry = isBinaryPayload && payloadBytes.length > 0
          ? parseTelemetryPayload(payloadBytes)
          : normalizeJsonTelemetryPayload(payloadValue);
        return telemetry?.payloadPreview ?? "Telemetry update";
      }
    case portNums.neighborinfoApp:
      {
        const neighborInfo = isBinaryPayload && payloadBytes.length > 0
          ? parseNeighborInfoPayload(payloadBytes, resolveJsonNodeNumber(root, "from", "source"))
          : normalizeJsonNeighborInfoPayload(payloadValue, resolveJsonNodeNumber(root, "from", "source"));
        return buildNeighborInfoPreview(neighborInfo);
      }
    default:
      return `${getPortMetadata(portNumValue)?.packetType ?? "Unknown Packet"} payload (json)`;
  }
}

function normalizeJsonNodeInfoPayload(payloadValue, fallbackNodeNumber = null) {
  if (!payloadValue || typeof payloadValue !== "object") {
    return null;
  }

  return {
    nodeId: resolveJsonNodeId(payloadValue, "id", "nodeId", "node_id") ??
      normalizeNodeIdFromNumber(fallbackNodeNumber),
    shortName: getJsonString(payloadValue, "shortName", "short_name", "shortname"),
    longName: getJsonString(payloadValue, "longName", "long_name", "longname")
  };
}

function normalizeJsonPositionPayload(payloadValue) {
  if (!payloadValue || typeof payloadValue !== "object") {
    return null;
  }

  const latitude = normalizeJsonCoordinate(
    getJsonNumber(payloadValue, "latitude", "lat"),
    getJsonNumber(payloadValue, "latitude_i", "latitudeI"));
  const longitude = normalizeJsonCoordinate(
    getJsonNumber(payloadValue, "longitude", "lon"),
    getJsonNumber(payloadValue, "longitude_i", "longitudeI"));

  if (latitude === null || longitude === null) {
    return null;
  }

  return {
    latitude,
    longitude
  };
}

function normalizeJsonTelemetryPayload(payloadValue) {
  if (!payloadValue || typeof payloadValue !== "object") {
    return null;
  }

  const deviceMetricsValue = getJsonProperty(payloadValue, "deviceMetrics", "device_metrics");
  const environmentMetricsValue = getJsonProperty(payloadValue, "environmentMetrics", "environment_metrics");
  const deviceSource = deviceMetricsValue && typeof deviceMetricsValue === "object"
    ? deviceMetricsValue
    : payloadValue;
  const environmentSource = environmentMetricsValue && typeof environmentMetricsValue === "object"
    ? environmentMetricsValue
    : payloadValue;
  const deviceMetrics = {
    batteryLevelPercent: normalizeJsonMetricNumber(deviceSource, "batteryLevelPercent", "battery_level_percent", "batteryLevel", "battery_level"),
    voltage: normalizeJsonMetricNumber(deviceSource, "voltage"),
    channelUtilization: normalizeJsonMetricNumber(deviceSource, "channelUtilization", "channel_utilization"),
    airUtilTx: normalizeJsonMetricNumber(deviceSource, "airUtilTx", "air_util_tx"),
    uptimeSeconds: normalizeJsonMetricNumber(deviceSource, "uptimeSeconds", "uptime_seconds")
  };
  const environmentMetrics = {
    temperatureCelsius: normalizeJsonMetricNumber(environmentSource, "temperatureCelsius", "temperature_celsius", "temperature"),
    relativeHumidity: normalizeJsonMetricNumber(environmentSource, "relativeHumidity", "relative_humidity"),
    barometricPressure: normalizeJsonMetricNumber(environmentSource, "barometricPressure", "barometric_pressure")
  };

  return {
    ...deviceMetrics,
    ...environmentMetrics,
    payloadPreview: buildTelemetryPreview(deviceMetrics, environmentMetrics)
  };
}

function normalizeJsonNeighborInfoPayload(payloadValue, fallbackReportingNodeNumber = null) {
  if (!payloadValue || typeof payloadValue !== "object") {
    return null;
  }

  const neighborsValue = getJsonProperty(payloadValue, "neighbors");
  const neighbors = Array.isArray(neighborsValue)
    ? neighborsValue
      .map(normalizeJsonNeighborEntry)
      .filter((entry) => entry?.nodeId)
    : [];

  return {
    reportingNodeId: resolveJsonNodeId(
      payloadValue,
      "reportingNodeId",
      "reporting_node_id",
      "nodeId",
      "node_id") ??
      normalizeNodeIdFromNumber(fallbackReportingNodeNumber),
    neighbors
  };
}

function normalizeJsonNeighborEntry(payloadValue) {
  if (!payloadValue || typeof payloadValue !== "object") {
    return null;
  }

  return {
    nodeId: resolveJsonNodeId(payloadValue, "nodeId", "node_id", "id"),
    snrDb: normalizeJsonMetricNumber(payloadValue, "snrDb", "snr_db", "snr"),
    lastRxAtUtc: normalizeJsonTimestamp(
      getJsonProperty(payloadValue, "lastRxAtUtc", "last_rx_at_utc", "lastRxAt", "last_rx_at", "lastRxTime", "last_rx_time"))
  };
}

function normalizeJsonRoutingPayload(payloadValue) {
  if (!payloadValue || typeof payloadValue !== "object") {
    return null;
  }

  const routeRequest = normalizeJsonRouteDiscovery(
    getJsonProperty(payloadValue, "routeRequest", "route_request"));
  if (routeRequest) {
    return {
      kind: "routeRequest",
      errorCode: null,
      errorName: null,
      ...routeRequest
    };
  }

  const routeReply = normalizeJsonRouteDiscovery(
    getJsonProperty(payloadValue, "routeReply", "route_reply"));
  if (routeReply) {
    return {
      kind: "routeReply",
      errorCode: null,
      errorName: null,
      ...routeReply
    };
  }

  const errorInfo = normalizeJsonRoutingError(
    getJsonProperty(payloadValue, "errorReason", "error_reason", "error"));
  if (errorInfo) {
    return {
      kind: "errorReason",
      errorCode: errorInfo.code,
      errorName: errorInfo.name,
      routeNodeIds: [],
      snrTowards: [],
      routeBackNodeIds: [],
      snrBack: []
    };
  }

  const fallbackDiscovery = normalizeJsonRouteDiscovery(payloadValue);
  return fallbackDiscovery
    ? {
        kind: "routeRequest",
        errorCode: null,
        errorName: null,
        ...fallbackDiscovery
      }
    : null;
}

function normalizeJsonRouteDiscovery(payloadValue) {
  if (!payloadValue || typeof payloadValue !== "object") {
    return null;
  }

  const routeNodeIds = normalizeJsonNodeIdArray(getJsonProperty(payloadValue, "route"));
  const snrTowards = normalizeJsonIntegerArray(getJsonProperty(payloadValue, "snrTowards", "snr_towards"));
  const routeBackNodeIds = normalizeJsonNodeIdArray(getJsonProperty(payloadValue, "routeBack", "route_back"));
  const snrBack = normalizeJsonIntegerArray(getJsonProperty(payloadValue, "snrBack", "snr_back"));

  return routeNodeIds.length > 0 || routeBackNodeIds.length > 0 || snrTowards.length > 0 || snrBack.length > 0
    ? {
        routeNodeIds,
        snrTowards,
        routeBackNodeIds,
        snrBack
      }
    : null;
}

function normalizeJsonRoutingError(value) {
  if (Number.isFinite(value)) {
    const code = Number(value);
    return {
      code,
      name: routingErrorNamesByCode[code] ?? `UNKNOWN_${code}`
    };
  }

  if (typeof value !== "string") {
    return null;
  }

  const normalizedToken = normalizeToken(value);
  for (const [codeText, errorName] of Object.entries(routingErrorNamesByCode)) {
    if (normalizeToken(errorName) === normalizedToken) {
      return {
        code: Number.parseInt(codeText, 10),
        name: errorName
      };
    }
  }

  return {
    code: null,
    name: normalizeText(value) ?? value
  };
}

function normalizeJsonNodeIdArray(value) {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .map((item) => {
      if (typeof item === "string") {
        return normalizeNodeId(item);
      }

      if (Number.isFinite(item)) {
        return normalizeNodeIdFromNumber(Number(item));
      }

      return null;
    })
    .filter((item) => typeof item === "string" && item.length > 0);
}

function normalizeJsonIntegerArray(value) {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .map((item) => {
      if (Number.isFinite(item)) {
        return Math.trunc(Number(item));
      }

      if (typeof item === "string") {
        const parsed = Number.parseInt(item, 10);
        return Number.isFinite(parsed) ? parsed : null;
      }

      return null;
    })
    .filter((item) => Number.isFinite(item));
}

function decodeJsonTextPayload(payloadValue, payloadBytes) {
  const text = decodeJsonTextValue(payloadValue);

  if (text) {
    return normalizePreviewText(text);
  }

  return decodeTextPayload(payloadBytes);
}

function decodeJsonTextValue(payloadValue) {
  if (typeof payloadValue === "string") {
    const decodedBase64 = tryDecodeBase64Strict(payloadValue);
    if (decodedBase64) {
      try {
        return textDecoder.decode(decodedBase64);
      } catch {
        return payloadValue;
      }
    }

    return payloadValue;
  }

  if (payloadValue && typeof payloadValue === "object") {
    return getJsonString(payloadValue, "text", "message");
  }

  return null;
}

function parseJsonPortNumValue(value) {
  if (Number.isFinite(value)) {
    return Number(value);
  }

  if (typeof value === "string") {
    const numericValue = Number.parseInt(value, 10);
    if (Number.isFinite(numericValue)) {
      return numericValue;
    }

    const normalized = normalizeToken(value);
    switch (normalized) {
      case "textmessageapp":
      case "text":
        return portNums.textMessageApp;
      case "positionapp":
      case "position":
        return portNums.positionApp;
      case "nodeinfoapp":
      case "nodeinfo":
        return portNums.nodeinfoApp;
      case "routingapp":
      case "routing":
        return portNums.routingApp;
      case "telemetryapp":
      case "telemetry":
        return portNums.telemetryApp;
      case "tracerouteapp":
      case "traceroute":
        return portNums.tracerouteApp;
      case "neighborinfoapp":
      case "neighborinfo":
        return portNums.neighborinfoApp;
      case "mapreportapp":
      case "mapreport":
        return portNums.mapReportApp;
    }
  }

  return null;
}

function mapJsonTypeToPortNum(value) {
  switch (normalizeToken(value)) {
    case "text":
    case "sendtext":
    case "textmessage":
    case "textmessageapp":
      return portNums.textMessageApp;
    case "position":
    case "positionupdate":
    case "positionapp":
      return portNums.positionApp;
    case "nodeinfo":
    case "nodeinformation":
    case "nodeinfoapp":
      return portNums.nodeinfoApp;
    case "routing":
    case "routingapp":
      return portNums.routingApp;
    case "telemetry":
    case "telemetryapp":
      return portNums.telemetryApp;
    case "traceroute":
    case "tracerouteapp":
      return portNums.tracerouteApp;
    case "neighborinfo":
    case "neighborinfoapp":
      return portNums.neighborinfoApp;
    case "mapreport":
    case "mapreportapp":
      return portNums.mapReportApp;
    default:
      return null;
  }
}

function resolveJsonNodeNumber(root, ...names) {
  for (const name of names) {
    const directValue = getJsonProperty(root, name);
    const directNumber = normalizeJsonNodeNumber(directValue);
    if (directNumber !== null) {
      return directNumber;
    }

    const decoded = getJsonProperty(root, "decoded");
    if (decoded && typeof decoded === "object") {
      const decodedValue = getJsonProperty(decoded, name);
      const decodedNumber = normalizeJsonNodeNumber(decodedValue);
      if (decodedNumber !== null) {
        return decodedNumber;
      }
    }
  }

  return null;
}

function normalizeJsonNodeNumber(value) {
  if (Number.isFinite(value) && value > 0) {
    return Number(value);
  }

  if (typeof value === "string") {
    const normalizedValue = normalizeText(value);
    if (!normalizedValue) {
      return null;
    }

    if (normalizedValue.startsWith("!")) {
      const numericValue = Number.parseInt(normalizedValue.slice(1), 16);
      return Number.isFinite(numericValue) && numericValue > 0
        ? numericValue
        : null;
    }

    const numericValue = Number.parseInt(normalizedValue, 10);
    return Number.isFinite(numericValue) && numericValue > 0
      ? numericValue
      : null;
  }

  return null;
}

function resolveJsonNodeId(target, ...names) {
  if (!target || typeof target !== "object") {
    return null;
  }

  for (const name of names) {
    const value = getJsonProperty(target, name);
    if (value === undefined) {
      continue;
    }

    if (typeof value === "string") {
      const normalized = normalizeNodeId(value);
      if (normalized) {
        return normalized;
      }
    }

    if (Number.isFinite(value)) {
      const normalized = normalizeNodeIdFromNumber(Number(value));
      if (normalized) {
        return normalized;
      }
    }
  }

  return null;
}

function resolveJsonNodeIdFromRoot(payloadValue) {
  return resolveJsonNodeId(payloadValue, "sender", "fromId", "from_id", "from");
}

function resolveJsonNodeIdFromTopic(sourceTopic) {
  const normalizedNodeId = normalizeText(sourceTopic)
    ? tryExtractNodeIdFromJsonTopic(sourceTopic)
    : null;

  return normalizedNodeId && normalizedNodeId !== "broker"
    ? normalizedNodeId
    : null;
}

function normalizeJsonCoordinate(decimalValue, scaledIntegerValue) {
  if (Number.isFinite(decimalValue)) {
    return Number(decimalValue);
  }

  if (Number.isFinite(scaledIntegerValue)) {
    return Number(scaledIntegerValue) / 10000000;
  }

  return null;
}

function normalizeJsonMetricNumber(target, ...names) {
  if (!target || typeof target !== "object") {
    return null;
  }

  return normalizeNumericMetric(getJsonNumber(target, ...names));
}

function normalizeJsonTimestamp(value) {
  if (typeof value === "string") {
    const normalizedText = normalizeText(value);
    if (!normalizedText) {
      return null;
    }

    if (/^\d+$/.test(normalizedText)) {
      const numericValue = Number.parseInt(normalizedText, 10);
      return normalizeUnixTimestamp(numericValue);
    }

    const parsedDate = Date.parse(normalizedText);
    return Number.isFinite(parsedDate)
      ? new Date(parsedDate).toISOString()
      : null;
  }

  if (Number.isFinite(value)) {
    return normalizeUnixTimestamp(Number(value));
  }

  return null;
}

function normalizeUnixTimestamp(value) {
  if (!Number.isFinite(value) || value <= 0) {
    return null;
  }

  const milliseconds = value >= 1000000000000
    ? value
    : value * 1000;

  return new Date(milliseconds).toISOString();
}

function getJsonProperty(target, ...names) {
  if (!target || typeof target !== "object") {
    return undefined;
  }

  for (const name of names) {
    if (Object.prototype.hasOwnProperty.call(target, name)) {
      return target[name];
    }
  }

  return undefined;
}

function getJsonString(target, ...names) {
  const value = getJsonProperty(target, ...names);
  return typeof value === "string"
    ? normalizeText(value)
    : null;
}

function getJsonNumber(target, ...names) {
  const value = getJsonProperty(target, ...names);

  if (Number.isFinite(value)) {
    return Number(value);
  }

  if (typeof value === "string") {
    const numericValue = Number.parseFloat(value);
    return Number.isFinite(numericValue)
      ? numericValue
      : null;
  }

  return null;
}

function normalizeToken(value) {
  const normalized = normalizeText(value);
  return normalized
    ? normalized.replace(/[^a-z0-9]+/gi, "").toLowerCase()
    : "";
}

function tryDecodeBase64Strict(value) {
  if (typeof value !== "string") {
    return null;
  }

  const normalizedValue = value.trim();
  if (normalizedValue.length === 0 || normalizedValue.length % 4 !== 0) {
    return null;
  }

  if (!/^[A-Za-z0-9+/]+={0,2}$/.test(normalizedValue)) {
    return null;
  }

  return base64ToBytes(normalizedValue);
}

function tryExtractNodeIdFromJsonTopic(sourceTopic) {
  const normalizedTopic = normalizeText(sourceTopic);
  if (!normalizedTopic) {
    return null;
  }

  const segments = normalizedTopic
    .split("/")
    .map(segment => segment.trim())
    .filter(Boolean);
  const lastSegment = segments.at(-1);

  return lastSegment?.startsWith("!")
    ? lastSegment.toLowerCase()
    : null;
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

function tryCreateNodeProjectionEvent(rawPacket, dataMessage, portMetadata) {
  if (!rawPacket || typeof rawPacket !== "object" || !dataMessage || typeof dataMessage !== "object") {
    return null;
  }

  const lastHeardAtUtc = normalizeText(rawPacket.receivedAtUtc) ?? new Date().toISOString();
  const nodeNumber = dataMessage.sourceNodeNumber ?? rawPacket.fromNodeNumber ?? null;
  const nodeId = normalizeNodeIdFromNumber(nodeNumber);

  if (!nodeId) {
    return null;
  }

  const projection = {
    nodeId,
    nodeNumber,
    lastHeardAtUtc,
    lastHeardChannel: tryResolveChannelKeyFromTopic(rawPacket.sourceTopic),
    lastTextMessageAtUtc: dataMessage.portNumValue === portNums.textMessageApp
      ? lastHeardAtUtc
      : null,
    packetType: portMetadata.packetType,
    payloadPreview: null,
    shortName: null,
    longName: null,
    lastKnownLatitude: null,
    lastKnownLongitude: null,
    batteryLevelPercent: null,
    voltage: null,
    channelUtilization: null,
    airUtilTx: null,
    uptimeSeconds: null,
    temperatureCelsius: null,
    relativeHumidity: null,
    barometricPressure: null
  };

  let payloadPreview = dataMessage.portNumValue === portNums.textMessageApp
    ? decodeTextPayload(dataMessage.payloadBytes)
    : null;

  switch (dataMessage.portNumValue) {
    case portNums.nodeinfoApp:
      {
        const nodeInfo = parseNodeInfoPayload(dataMessage.payloadBytes);
        if (nodeInfo) {
          projection.nodeId = nodeInfo.nodeId ?? projection.nodeId;
          projection.shortName = nodeInfo.shortName;
          projection.longName = nodeInfo.longName;
          payloadPreview = buildNodeInfoPreview(nodeInfo);
        }
      }
      break;
    case portNums.positionApp:
      {
        const position = parsePositionPayload(dataMessage.payloadBytes);
        if (position) {
          projection.lastKnownLatitude = position.latitude;
          projection.lastKnownLongitude = position.longitude;
          payloadPreview = buildPositionPreview(position);
        }
      }
      break;
    case portNums.telemetryApp:
      {
        const telemetry = parseTelemetryPayload(dataMessage.payloadBytes);
        if (telemetry) {
          projection.batteryLevelPercent = telemetry.batteryLevelPercent;
          projection.voltage = telemetry.voltage;
          projection.channelUtilization = telemetry.channelUtilization;
          projection.airUtilTx = telemetry.airUtilTx;
          projection.uptimeSeconds = telemetry.uptimeSeconds;
          projection.temperatureCelsius = telemetry.temperatureCelsius;
          projection.relativeHumidity = telemetry.relativeHumidity;
          projection.barometricPressure = telemetry.barometricPressure;
          payloadPreview = telemetry.payloadPreview;
        }
      }
      break;
    case portNums.neighborinfoApp:
      {
        const neighborInfo = parseNeighborInfoPayload(dataMessage.payloadBytes, nodeNumber);
        if (neighborInfo) {
          projection.nodeId = neighborInfo.reportingNodeId ?? projection.nodeId;
          payloadPreview = buildNeighborInfoPreview(neighborInfo);
        }
      }
      break;
  }

  projection.payloadPreview = payloadPreview ??
    buildPayloadPreview(portMetadata, dataMessage.payloadBytes);

  return {
    nodeProjection: projection,
    payloadPreview: projection.payloadPreview
  };
}

function tryCreateNeighborInfoEvent(rawPacket, dataMessage) {
  if (!rawPacket || typeof rawPacket !== "object" || !dataMessage || typeof dataMessage !== "object") {
    return null;
  }

  if (dataMessage.portNumValue !== portNums.neighborinfoApp) {
    return null;
  }

  const fallbackNodeNumber = dataMessage.sourceNodeNumber ?? rawPacket.fromNodeNumber ?? null;
  const neighborInfo = parseNeighborInfoPayload(dataMessage.payloadBytes, fallbackNodeNumber);

  if (!neighborInfo?.reportingNodeId) {
    return null;
  }

  return {
    neighborInfo: {
      reportingNodeId: neighborInfo.reportingNodeId,
      neighbors: neighborInfo.neighbors.map((neighbor) => ({
        nodeId: neighbor.nodeId,
        snrDb: neighbor.snrDb,
        lastRxAtUtc: neighbor.lastRxAtUtc
      }))
    },
    payloadPreview: buildNeighborInfoPreview(neighborInfo)
  };
}

function parseNodeInfoPayload(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return null;
  }

  let offset = 0;
  let nodeId = null;
  let shortName = null;
  let longName = null;
  let hasKnownField = false;

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
      case 2:
      case 3:
        if (wireType !== 2) {
          return null;
        }

        {
          const field = readLengthDelimited(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          const fieldText = decodeProtoString(field.value);

          if (fieldNumber === 1) {
            nodeId = normalizeNodeId(fieldText);
          } else if (fieldNumber === 2) {
            longName = fieldText;
          } else {
            shortName = fieldText;
          }

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

  if (!hasKnownField) {
    return null;
  }

  return {
    nodeId,
    shortName,
    longName
  };
}

function parsePositionPayload(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return null;
  }

  let offset = 0;
  let latitude = null;
  let longitude = null;
  let hasKnownField = false;

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
      case 2:
        if (wireType !== 5) {
          return null;
        }

        {
          const field = readSFixed32(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          const coordinate = field.value / 10000000;

          if (fieldNumber === 1) {
            latitude = coordinate;
          } else {
            longitude = coordinate;
          }

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

  if (!hasKnownField) {
    return null;
  }

  if (latitude === 0 && longitude === 0) {
    return {
      latitude: null,
      longitude: null
    };
  }

  return {
    latitude,
    longitude
  };
}

function parseTelemetryPayload(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return null;
  }

  let offset = 0;
  let deviceMetricsBytes = null;
  let environmentMetricsBytes = null;
  let hasKnownField = false;

  while (offset < payloadBytes.length) {
    const tag = readVarint(payloadBytes, offset);
    if (!tag) {
      return null;
    }

    offset = tag.nextOffset;
    const fieldNumber = Number(tag.value >> 3n);
    const wireType = Number(tag.value & 7n);

    switch (fieldNumber) {
      case 2:
      case 8:
        if (wireType !== 2) {
          return null;
        }

        {
          const field = readLengthDelimited(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;

          if (fieldNumber === 2) {
            deviceMetricsBytes = field.value;
          } else {
            environmentMetricsBytes = field.value;
          }

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

  if (!hasKnownField) {
    return null;
  }

  const deviceMetrics = parseDeviceMetrics(deviceMetricsBytes);
  const environmentMetrics = parseEnvironmentMetrics(environmentMetricsBytes);
  const payloadPreview = buildTelemetryPreview(deviceMetrics, environmentMetrics);

  return {
    batteryLevelPercent: deviceMetrics?.batteryLevelPercent ?? null,
    voltage: deviceMetrics?.voltage ?? null,
    channelUtilization: deviceMetrics?.channelUtilization ?? null,
    airUtilTx: deviceMetrics?.airUtilTx ?? null,
    uptimeSeconds: deviceMetrics?.uptimeSeconds ?? null,
    temperatureCelsius: environmentMetrics?.temperatureCelsius ?? null,
    relativeHumidity: environmentMetrics?.relativeHumidity ?? null,
    barometricPressure: environmentMetrics?.barometricPressure ?? null,
    payloadPreview
  };
}

function parseNeighborInfoPayload(payloadBytes, fallbackReportingNodeNumber = null) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return null;
  }

  let offset = 0;
  let hasKnownField = false;
  let reportingNodeId = null;
  const neighbors = [];

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
      case 2:
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
          const numericValue = numberOrNull(field.value);

          if (fieldNumber === 1) {
            reportingNodeId = normalizeNodeIdFromNumber(numericValue);
          }

          hasKnownField = true;
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
          const neighbor = parseNeighborEntryPayload(field.value);

          if (neighbor?.nodeId) {
            neighbors.push(neighbor);
          }

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

  if (!hasKnownField) {
    return null;
  }

  return {
    reportingNodeId: reportingNodeId ?? normalizeNodeIdFromNumber(fallbackReportingNodeNumber),
    neighbors
  };
}

function parseNeighborEntryPayload(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return null;
  }

  let offset = 0;
  let hasKnownField = false;
  let nodeId = null;
  let snrDb = null;
  let lastRxAtUtc = null;

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
          const numericValue = numberOrNull(field.value);

          nodeId = normalizeNodeIdFromNumber(numericValue);
          hasKnownField = true;
        }
        break;
      case 2:
        if (wireType !== 5) {
          return null;
        }

        {
          const field = readFloat32(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          snrDb = normalizeSnrDb(field.value);
          hasKnownField = true;
        }
        break;
      case 3:
        if (wireType !== 5) {
          return null;
        }

        {
          const field = readFixed32(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          lastRxAtUtc = normalizeUnixTimestampSeconds(field.value);
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

  if (!hasKnownField) {
    return null;
  }

  return {
    nodeId,
    snrDb,
    lastRxAtUtc
  };
}

function parseRoutingPayload(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return null;
  }

  let offset = 0;
  let hasKnownField = false;
  let routingInfo = null;

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
          const routeDiscovery = parseRouteDiscoveryPayload(field.value);
          if (!routeDiscovery) {
            return null;
          }

          routingInfo = {
            kind: fieldNumber === 1 ? "routeRequest" : "routeReply",
            errorCode: null,
            errorName: null,
            routeNodeIds: routeDiscovery.routeNodeIds,
            snrTowards: routeDiscovery.snrTowards,
            routeBackNodeIds: routeDiscovery.routeBackNodeIds,
            snrBack: routeDiscovery.snrBack
          };
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
          const errorCode = numberOrNull(field.value);
          if (errorCode === null) {
            return null;
          }

          routingInfo = {
            kind: "errorReason",
            errorCode,
            errorName: routingErrorNamesByCode[errorCode] ?? `UNKNOWN_${errorCode}`,
            routeNodeIds: [],
            snrTowards: [],
            routeBackNodeIds: [],
            snrBack: []
          };
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

  return hasKnownField ? routingInfo : null;
}

function parseRouteDiscoveryPayload(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return null;
  }

  let offset = 0;
  let hasKnownField = false;
  const routeNodeIds = [];
  const snrTowards = [];
  const routeBackNodeIds = [];
  const snrBack = [];

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
      case 3:
        if (wireType === 5) {
          const field = readFixed32(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          const nodeId = normalizeNodeIdFromNumber(field.value);
          if (nodeId) {
            if (fieldNumber === 1) {
              routeNodeIds.push(nodeId);
            } else {
              routeBackNodeIds.push(nodeId);
            }
          }

          hasKnownField = true;
          break;
        }

        if (wireType === 2) {
          const field = readLengthDelimited(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          const nodeIds = parsePackedFixed32NodeIds(field.value);
          if (nodeIds === null) {
            return null;
          }

          if (fieldNumber === 1) {
            routeNodeIds.push(...nodeIds);
          } else {
            routeBackNodeIds.push(...nodeIds);
          }

          hasKnownField = true;
          break;
        }

        return null;
      case 2:
      case 4:
        if (wireType === 0) {
          const field = readVarint(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          const snrValue = int32OrNull(field.value);
          if (snrValue === null) {
            return null;
          }

          if (fieldNumber === 2) {
            snrTowards.push(snrValue);
          } else {
            snrBack.push(snrValue);
          }

          hasKnownField = true;
          break;
        }

        if (wireType === 2) {
          const field = readLengthDelimited(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          const snrValues = parsePackedInt32Values(field.value);
          if (snrValues === null) {
            return null;
          }

          if (fieldNumber === 2) {
            snrTowards.push(...snrValues);
          } else {
            snrBack.push(...snrValues);
          }

          hasKnownField = true;
          break;
        }

        return null;
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

  if (!hasKnownField) {
    return null;
  }

  return {
    routeNodeIds,
    snrTowards,
    routeBackNodeIds,
    snrBack
  };
}

function parsePackedFixed32NodeIds(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length % 4 !== 0) {
    return null;
  }

  const nodeIds = [];

  for (let offset = 0; offset < payloadBytes.length; offset += 4) {
    const field = readFixed32(payloadBytes, offset);
    if (!field) {
      return null;
    }

    const nodeId = normalizeNodeIdFromNumber(field.value);
    if (nodeId) {
      nodeIds.push(nodeId);
    }
  }

  return nodeIds;
}

function parsePackedInt32Values(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array)) {
    return null;
  }

  let offset = 0;
  const values = [];

  while (offset < payloadBytes.length) {
    const field = readVarint(payloadBytes, offset);
    if (!field) {
      return null;
    }

    offset = field.nextOffset;
    const numericValue = int32OrNull(field.value);
    if (numericValue === null) {
      return null;
    }

    values.push(numericValue);
  }

  return values;
}

function parseDeviceMetrics(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return null;
  }

  let offset = 0;
  let hasKnownField = false;
  let batteryLevelPercent = null;
  let voltage = null;
  let channelUtilization = null;
  let airUtilTx = null;
  let uptimeSeconds = null;

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
      case 12:
        if (wireType !== 0) {
          return null;
        }

        {
          const field = readVarint(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          const numericValue = numberOrNull(field.value);

          if (fieldNumber === 1) {
            batteryLevelPercent = numericValue;
          } else {
            uptimeSeconds = numericValue;
          }

          hasKnownField = true;
        }
        break;
      case 2:
      case 3:
      case 4:
        if (wireType !== 5) {
          return null;
        }

        {
          const field = readFloat32(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;

          if (fieldNumber === 2) {
            voltage = field.value;
          } else if (fieldNumber === 3) {
            channelUtilization = field.value;
          } else {
            airUtilTx = field.value;
          }

          hasKnownField = true;
        }
        break;
      case 3:
        if (wireType !== 5) {
          return null;
        }

        {
          const field = readFixed32(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;
          lastRxAtUtc = normalizeUnixTimestampSeconds(field.value);
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

  if (!hasKnownField) {
    return null;
  }

  const normalizedBatteryLevelPercent = typeof batteryLevelPercent === "number" &&
      Number.isFinite(batteryLevelPercent)
    ? (batteryLevelPercent === 0 && (voltage === null || voltage === 0) ? null : batteryLevelPercent)
    : null;

  return {
    batteryLevelPercent: normalizedBatteryLevelPercent,
    voltage: normalizeNumericMetric(voltage),
    channelUtilization: normalizeNumericMetric(channelUtilization),
    airUtilTx: normalizeNumericMetric(airUtilTx),
    uptimeSeconds: normalizeNumericMetric(uptimeSeconds)
  };
}

function parseEnvironmentMetrics(payloadBytes) {
  if (!(payloadBytes instanceof Uint8Array) || payloadBytes.length === 0) {
    return null;
  }

  let offset = 0;
  let hasKnownField = false;
  let temperatureCelsius = null;
  let relativeHumidity = null;
  let barometricPressure = null;

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
      case 2:
      case 5:
        if (wireType !== 5) {
          return null;
        }

        {
          const field = readFloat32(payloadBytes, offset);
          if (!field) {
            return null;
          }

          offset = field.nextOffset;

          if (fieldNumber === 1) {
            temperatureCelsius = field.value;
          } else if (fieldNumber === 2) {
            relativeHumidity = field.value;
          } else {
            barometricPressure = field.value;
          }

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

  if (!hasKnownField) {
    return null;
  }

  return {
    temperatureCelsius: normalizeNumericMetric(temperatureCelsius),
    relativeHumidity: normalizeNumericMetric(relativeHumidity),
    barometricPressure: normalizeNumericMetric(barometricPressure)
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

function readSFixed32(bytes, offset) {
  if (!(bytes instanceof Uint8Array) || offset < 0 || offset + 4 > bytes.length) {
    return null;
  }

  const view = new DataView(bytes.buffer, bytes.byteOffset + offset, 4);
  return {
    value: view.getInt32(0, true),
    nextOffset: offset + 4
  };
}

function readFloat32(bytes, offset) {
  if (!(bytes instanceof Uint8Array) || offset < 0 || offset + 4 > bytes.length) {
    return null;
  }

  const view = new DataView(bytes.buffer, bytes.byteOffset + offset, 4);
  return {
    value: view.getFloat32(0, true),
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

function int32OrNull(value) {
  if (typeof value !== "bigint") {
    return null;
  }

  return Number(BigInt.asIntN(32, value));
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

  if (portMetadata.portNumValue === portNums.routingApp) {
    const routingInfo = parseRoutingPayload(payloadBytes);
    return routingInfo
      ? buildRoutingPreview(routingInfo)
      : "Routing update";
  }

  if (portMetadata.portNumValue === portNums.tracerouteApp) {
    const routeDiscovery = parseRouteDiscoveryPayload(payloadBytes);
    return routeDiscovery
      ? buildTraceroutePreview(routeDiscovery, null, null)
      : "Traceroute";
  }

  if (portMetadata.portNumValue === portNums.neighborinfoApp) {
    const neighborInfo = parseNeighborInfoPayload(payloadBytes);
    return neighborInfo
      ? buildNeighborInfoPreview(neighborInfo)
      : "Neighbor info update";
  }

  return `${portMetadata.packetType} payload (${payloadBytes.length} bytes)`;
}

function buildNodeInfoPreview(nodeInfo) {
  if (!nodeInfo || typeof nodeInfo !== "object") {
    return "Node info update";
  }

  if (nodeInfo.longName && nodeInfo.shortName) {
    return `Node info: ${nodeInfo.longName} (${nodeInfo.shortName})`;
  }

  if (nodeInfo.longName) {
    return `Node info: ${nodeInfo.longName}`;
  }

  if (nodeInfo.shortName) {
    return `Node info: ${nodeInfo.shortName}`;
  }

  return "Node info update";
}

function buildPositionPreview(position) {
  if (!position || typeof position !== "object" || position.latitude === null || position.longitude === null) {
    return "Position update";
  }

  return `Position: ${position.latitude.toFixed(5)}, ${position.longitude.toFixed(5)}`;
}

function buildTelemetryPreview(deviceMetrics, environmentMetrics) {
  const parts = [];

  if (deviceMetrics) {
    if (deviceMetrics.batteryLevelPercent !== null) {
      parts.push(`${deviceMetrics.batteryLevelPercent}% battery`);
    }

    if (deviceMetrics.voltage !== null) {
      parts.push(`${deviceMetrics.voltage.toFixed(2)}V`);
    }

    if (deviceMetrics.channelUtilization !== null) {
      parts.push(`${deviceMetrics.channelUtilization.toFixed(1)}% channel`);
    }

    if (deviceMetrics.airUtilTx !== null) {
      parts.push(`${deviceMetrics.airUtilTx.toFixed(1)}% TX`);
    }

    if (deviceMetrics.uptimeSeconds !== null) {
      parts.push(`uptime ${formatDuration(deviceMetrics.uptimeSeconds)}`);
    }
  }

  if (parts.length > 0) {
    return `Device metrics: ${parts.join(", ")}`;
  }

  if (environmentMetrics) {
    if (environmentMetrics.temperatureCelsius !== null) {
      parts.push(`${environmentMetrics.temperatureCelsius.toFixed(1)}C`);
    }

    if (environmentMetrics.relativeHumidity !== null) {
      parts.push(`${environmentMetrics.relativeHumidity.toFixed(1)}% RH`);
    }

    if (environmentMetrics.barometricPressure !== null) {
      parts.push(`${environmentMetrics.barometricPressure.toFixed(1)} hPa`);
    }
  }

  return parts.length > 0
    ? `Environment metrics: ${parts.join(", ")}`
    : "Telemetry update";
}

function buildNeighborInfoPreview(neighborInfo) {
  if (!neighborInfo || typeof neighborInfo !== "object" || !Array.isArray(neighborInfo.neighbors)) {
    return "Neighbor info update";
  }

  const neighborCount = neighborInfo.neighbors.length;
  const suffix = neighborCount === 1 ? "neighbor" : "neighbors";
  return `Neighbor info: ${neighborCount} ${suffix} reported`;
}

function buildRoutingPreview(routingInfo) {
  if (!routingInfo || typeof routingInfo !== "object") {
    return "Routing update";
  }

  if (routingInfo.kind === "errorReason") {
    return `Routing error: ${routingInfo.errorName ?? "UNKNOWN"}`;
  }

  const prefix = routingInfo.kind === "routeReply"
    ? "Routing route reply"
    : "Routing route request";
  const routeNodeIds = Array.isArray(routingInfo.routeNodeIds)
    ? routingInfo.routeNodeIds.filter((nodeId) => typeof nodeId === "string" && nodeId.length > 0)
    : [];
  const routeBackNodeIds = Array.isArray(routingInfo.routeBackNodeIds)
    ? routingInfo.routeBackNodeIds.filter((nodeId) => typeof nodeId === "string" && nodeId.length > 0)
    : [];
  const parts = [];

  if (routeNodeIds.length > 0) {
    parts.push(routeNodeIds.join(" -> "));
  }

  if (routeBackNodeIds.length > 0) {
    parts.push(`return ${routeBackNodeIds.join(" -> ")}`);
  }

  return parts.length > 0
    ? `${prefix}: ${parts.join("; ")}`
    : prefix;
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

function tryResolveChannelKeyFromTopic(sourceTopic) {
  const normalizedTopic = normalizeText(sourceTopic);
  if (!normalizedTopic) {
    return null;
  }

  const segments = normalizedTopic
    .split("/")
    .map(segment => segment.trim())
    .filter(Boolean);

  if (segments.length < 5 || !equalsIgnoreCase(segments[0], "msh")) {
    return null;
  }

  const region = normalizeText(segments[1]);
  const channel = normalizeText(segments[4]);

  if (!region || !channel || channel === "#" || channel === "+") {
    return null;
  }

  return `${region}/${channel}`;
}

function normalizeNodeId(value) {
  const normalizedValue = normalizeText(value);
  if (!normalizedValue) {
    return null;
  }

  if (normalizedValue.startsWith("!")) {
    const hexValue = normalizedValue.slice(1);
    if (/^[0-9a-f]+$/i.test(hexValue) && hexValue.length <= 8) {
      const numericValue = Number.parseInt(hexValue, 16);
      return Number.isFinite(numericValue)
        ? normalizeNodeIdFromNumber(numericValue)
        : normalizedValue.toLowerCase();
    }

    return normalizedValue.toLowerCase();
  }

  if (normalizedValue.startsWith("0x")) {
    const numericValue = Number.parseInt(normalizedValue.slice(2), 16);
    return Number.isFinite(numericValue) ? normalizeNodeIdFromNumber(numericValue) : normalizedValue;
  }

  const numericValue = Number.parseInt(normalizedValue, 10);
  return Number.isFinite(numericValue) ? normalizeNodeIdFromNumber(numericValue) : normalizedValue;
}

function normalizeNodeIdFromNumber(value) {
  if (!Number.isSafeInteger(value) || value <= 0) {
    return null;
  }

  return `!${value.toString(16).padStart(8, "0")}`;
}

function decodeProtoString(bytes) {
  if (!(bytes instanceof Uint8Array) || bytes.length === 0) {
    return null;
  }

  try {
    return normalizeText(textDecoder.decode(bytes));
  } catch {
    return null;
  }
}

function normalizeNumericMetric(value) {
  return typeof value === "number" && Number.isFinite(value) && value !== 0
    ? value
    : null;
}

function normalizeSnrDb(value) {
  return typeof value === "number" && Number.isFinite(value)
    ? value
    : null;
}

function normalizeUnixTimestampSeconds(value) {
  if (!Number.isFinite(value) || value <= 0) {
    return null;
  }

  return new Date(value * 1000).toISOString();
}

function formatDuration(totalSeconds) {
  if (!Number.isFinite(totalSeconds) || totalSeconds < 0) {
    return "0m";
  }

  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);

  if (days >= 1) {
    return `${days}d ${hours}h`;
  }

  if (hours >= 1) {
    return `${hours}h ${minutes}m`;
  }

  return `${minutes}m`;
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
