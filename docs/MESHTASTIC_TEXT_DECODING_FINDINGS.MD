# Meshtastic Text Packet Decoding Process (Protocol Handoff)

Source analyzed: `mqtt-connect.py` in this repo.

This describes **how decoding works**, independent of Python implementation, so it can be reproduced in C#.

## 1) Packet Layers

Inbound MQTT payload is a protobuf stack:

1. `ServiceEnvelope` (outer wrapper)
2. `MeshPacket` (`ServiceEnvelope.packet`)
3. `Data` (`MeshPacket.decoded` if cleartext, or decrypted bytes parsed as `Data`)
4. Text message bytes (`Data.payload`) when `Data.portnum == TEXT_MESSAGE_APP`

## 2) Decode Flow

Use this order:

1. Parse MQTT bytes as `ServiceEnvelope`.
2. Read `MeshPacket mp = serviceEnvelope.packet`.
3. If `mp.encrypted` exists **and** `mp.decoded` is absent:
   1. Decrypt `mp.encrypted` using AES-CTR (details below).
   2. Parse decrypted bytes as protobuf `Data`.
   3. Set/use that `Data` as decoded packet content.
4. If decoded `portnum` is `TEXT_MESSAGE_APP`:
   1. Interpret decoded `payload` as UTF-8 text.
   2. That string is the user message.

If packet is not text portnum, it is handled by a different protobuf type (node info, position, telemetry, etc.).

## 3) Crypto Inputs (Critical)

### Key normalization used before decryption

1. Read key as Base64 text.
2. If key is `"AQ=="`, replace it with `"1PG7OiApB1nwvP+rz05pAQ=="` (default channel key alias expansion used by this app).
3. Normalize URL-safe Base64 variants:
   1. Replace `-` with `+`
   2. Replace `_` with `/`
4. Add `=` padding so length is a multiple of 4.
5. Base64-decode to raw AES key bytes.

### Nonce/Counter block construction

AES mode is CTR with a 16-byte initial counter block:

- First 8 bytes: `packet.id` as **little-endian uint64**
- Next 8 bytes: `packet.from` as **little-endian uint64**

Concatenate in that order:

`nonce = LE64(packet_id) || LE64(from_node)`

Then decrypt:

`plaintext_data_bytes = AES-CTR(key_bytes, nonce).Decrypt(mp.encrypted)`

Parse `plaintext_data_bytes` as protobuf `Data`.

## 4) C# Implementation Notes

1. Endianness matters: both `id` and `from` are little-endian 8-byte values.
2. Do not UTF-8 decode before protobuf parsing; decrypt first, then parse as `Data`, then decode `Data.payload` as UTF-8 for text portnum.
3. Only attempt decrypt when `encrypted` is present and `decoded` is absent.
4. If decryption/protobuf parse fails, treat as undecodable packet (likely wrong key/topic/channel).
5. This app does **not** authenticate ciphertext (CTR only), so parse success is the practical check.

## 5) App-Specific Behavior Worth Knowing

1. Incoming MQTT subscription is already channel-scoped by topic (`root_topic + channel + "/#"`), and this code does not re-validate `channel_id` or packet channel hash during decode.
2. Duplicate text suppression is done by packet `id` in SQLite; that is post-decode behavior, not part of decryption.
3. There is a length guard (`len(mqtt_payload) > DATA_PAYLOAD_LEN`) that may drop large envelopes; this is a client choice, not core decode logic.

## 6) Minimal Decoder Pseudoflow

```text
service = Parse<ServiceEnvelope>(mqtt_payload)
mp = service.packet

if mp.has_encrypted and not mp.has_decoded:
    key_bytes = NormalizeAndDecodeKey(user_key)
    nonce = LE64(mp.id) + LE64(mp.from)
    decrypted = AES_CTR_Decrypt(key_bytes, nonce, mp.encrypted)
    decoded = Parse<Data>(decrypted)
else:
    decoded = mp.decoded

if decoded.portnum == TEXT_MESSAGE_APP:
    text = UTF8(decoded.payload)
```
