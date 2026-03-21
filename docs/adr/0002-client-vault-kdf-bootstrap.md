# ADR 0002: Client Vault KDF Bootstrap

- Status: Accepted
- Date: 2026-03-12

## Context

The browser-local key vault is required in wave 1.

Current fixed requirements are:

- keys are client-only and must never be sent to the server
- keys survive logout, but the vault re-locks on logout
- storage must be encrypted at rest behind a dedicated local passphrase
- the long-term preferred KDF is `Argon2id`

The first vault slice is intentionally a bootstrap slice:

- establish the IndexedDB envelope
- request persistent browser storage
- create a passphrase-protected vault manifest
- support lock and unlock without waiting for the later decrypt worker and runtime key pipeline

At the same time, the current client stack does not yet include a browser WASM crypto packaging path for an `Argon2id` dependency, and the first slice should stay small, standards-based, and easy to verify.

## Decision

For the first local vault envelope only, MeshBoard will use `PBKDF2-SHA-256` through the browser `Web Crypto` API as a bootstrap KDF.

Scope limits:

- this applies only to the first client-vault bootstrap slice
- it is acceptable for:
  - vault manifest creation
  - passphrase verification
  - initial wrapped-record envelope scaffolding
- it does not change the long-term target KDF; `Argon2id` remains the intended stronger follow-up

Implementation constraints:

- KDF metadata must be stored with each vault envelope version
- the record format must support later migration or re-wrapping to `Argon2id`
- only wrapped or encrypted blobs may be persisted
- plaintext key strings must never be written to IndexedDB
- runtime keys must still be imported into `Web Crypto` after unlock

## Consequences

### Positive

- the first vault slice can ship using browser-standard primitives only
- no extra WASM crypto dependency is required to establish the initial client vault boundary
- lock, unlock, persistence, and storage-durability behavior can be implemented and tested immediately

### Negative

- `PBKDF2` is weaker against GPU/ASIC password guessing than `Argon2id`
- a later migration slice is required to move the vault to the preferred KDF
- the bootstrap implementation must not be treated as the final security posture

### Follow-up

- add the stronger `Argon2id` client KDF slice with an open-source browser-compatible implementation
- migrate or re-wrap existing vault envelopes after the stronger KDF lands
- keep the vault manifest versioned so the upgrade path is explicit and testable
