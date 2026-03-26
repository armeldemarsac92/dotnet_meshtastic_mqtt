# ADR 0002: Browser Vault KDF Bootstrap

- Status: Accepted
- Date: 2026-03-12

## Context

The architecture roadmap sets the browser-local vault as a wave 1 requirement:

- keys must remain client-side
- the vault must be protected by a dedicated local passphrase
- encrypted-at-rest local storage must exist before decryption work proceeds

The roadmap also states that `Argon2id` is the preferred KDF for the production local vault.

The first bootstrap slice for the vault needs to land now, before the later worker-based decrypt pipeline and before a dedicated browser-side WASM crypto dependency is introduced. The current client build also has no general-purpose JavaScript bundling pipeline beyond Tailwind.

## Decision

The first browser vault bootstrap slice is allowed to use:

- `PBKDF2` via Web Crypto for passphrase derivation
- `AES-GCM` via Web Crypto for the vault-envelope verifier
- `IndexedDB` for local persistence

This decision is limited to the bootstrap envelope only:

- vault manifest
- passphrase verification record
- lock/unlock lifecycle
- persistent-storage status

This decision does not freeze the long-term KDF for wrapped Meshtastic key records.

`Argon2id` remains the target for the stronger follow-up slice that introduces:

- wrapped key blobs
- worker-based crypto hot paths
- versioned migration from the bootstrap envelope if needed

## Consequences

### Immediate consequences

- the client can establish a real local vault boundary without sending any key material to the API
- the bootstrap slice stays standards-based and does not require a WASM crypto package immediately
- the local vault manifest must record versioned KDF metadata so a future migration remains possible

### Near-term consequences

- a later vault-hardening slice must evaluate and integrate an open-source `Argon2id` browser implementation
- wrapped key-record persistence should migrate to the stronger KDF once that slice lands
- the decrypt worker contract remains unchanged by this temporary bootstrap choice

### Guardrails

- no raw decryption keys may be stored in the bootstrap slice
- no server API may receive the local vault passphrase
- logout must lock the vault even though the persisted manifest survives
- any later move from `PBKDF2` to `Argon2id` must preserve explicit versioning and migration behavior
