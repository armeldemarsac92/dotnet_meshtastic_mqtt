const dbName = "meshboard-client";
const dbVersion = 1;
const metaStoreName = "vault_meta";
const manifestKey = "manifest";
const vaultVerifier = "meshboard-vault:v1";
const pbkdf2Iterations = 310000;

let dbPromise;
let sessionUnlocked = false;

export async function createVault(passphrase) {
    ensurePassphrase(passphrase);

    const database = await getDatabase();
    const existingManifest = await readRecord(database, metaStoreName, manifestKey);
    if (existingManifest) {
        throw new Error("A local vault already exists on this device.");
    }

    const salt = crypto.getRandomValues(new Uint8Array(16));
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const wrappingKey = await deriveWrappingKey(passphrase, salt, pbkdf2Iterations);
    const cipherText = await encryptVerifier(wrappingKey, iv);

    const manifest = {
        key: manifestKey,
        version: 1,
        createdAtUtc: new Date().toISOString(),
        storedKeyCount: 0,
        kdf: {
            name: "PBKDF2",
            hash: "SHA-256",
            iterations: pbkdf2Iterations,
            saltBase64Url: toBase64Url(salt)
        },
        verification: {
            algorithm: "AES-GCM",
            ivBase64Url: toBase64Url(iv),
            cipherTextBase64Url: toBase64Url(new Uint8Array(cipherText))
        }
    };

    await writeRecord(database, metaStoreName, manifest);
    sessionUnlocked = true;
    return await buildStatus(database);
}

export async function getStatus() {
    const database = await getDatabase();
    return await buildStatus(database);
}

export async function lockVault() {
    sessionUnlocked = false;
    const database = await getDatabase();
    return await buildStatus(database);
}

export async function requestPersistentStorage() {
    if (navigator.storage && typeof navigator.storage.persisted === "function") {
        const persisted = await navigator.storage.persisted();
        if (!persisted && typeof navigator.storage.persist === "function") {
            await navigator.storage.persist();
        }
    }

    const database = await getDatabase();
    return await buildStatus(database);
}

export async function unlockVault(passphrase) {
    ensurePassphrase(passphrase);

    const database = await getDatabase();
    const manifest = await readManifest(database);
    if (!manifest) {
        throw new Error("No local vault is configured for this browser.");
    }

    const salt = fromBase64Url(manifest.kdf.saltBase64Url);
    const iv = fromBase64Url(manifest.verification.ivBase64Url);
    const cipherText = fromBase64Url(manifest.verification.cipherTextBase64Url);
    const wrappingKey = await deriveWrappingKey(passphrase, salt, manifest.kdf.iterations);
    const decrypted = await crypto.subtle.decrypt(
        { name: "AES-GCM", iv },
        wrappingKey,
        cipherText);

    const decodedVerifier = new TextDecoder().decode(decrypted);
    if (decodedVerifier !== vaultVerifier) {
        throw new Error("The vault passphrase is invalid.");
    }

    sessionUnlocked = true;
    return await buildStatus(database);
}

async function buildStatus(database) {
    const manifest = await readManifest(database);
    const persistentStorageSupported = Boolean(navigator.storage && typeof navigator.storage.persisted === "function");
    const persistentStorageGranted = persistentStorageSupported
        ? await navigator.storage.persisted()
        : false;
    const hasVault = manifest !== null;

    return {
        hasVault,
        isLocked: hasVault && !sessionUnlocked,
        isUnlocked: hasVault && sessionUnlocked,
        needsPassphraseSetup: !hasVault,
        persistentStorageSupported,
        persistentStorageGranted,
        storedKeyCount: manifest?.storedKeyCount ?? 0,
        kdfName: manifest?.kdf?.name ?? null
    };
}

async function deriveWrappingKey(passphrase, salt, iterations) {
    const passphraseBytes = new TextEncoder().encode(passphrase);
    const keyMaterial = await crypto.subtle.importKey(
        "raw",
        passphraseBytes,
        "PBKDF2",
        false,
        ["deriveKey"]);

    return await crypto.subtle.deriveKey(
        {
            name: "PBKDF2",
            salt,
            iterations,
            hash: "SHA-256"
        },
        keyMaterial,
        {
            name: "AES-GCM",
            length: 256
        },
        false,
        ["encrypt", "decrypt"]);
}

async function encryptVerifier(wrappingKey, iv) {
    return await crypto.subtle.encrypt(
        { name: "AES-GCM", iv },
        wrappingKey,
        new TextEncoder().encode(vaultVerifier));
}

function ensurePassphrase(passphrase) {
    if (typeof passphrase !== "string" || passphrase.trim().length < 12) {
        throw new Error("Vault passphrases must be at least 12 characters.");
    }
}

function fromBase64Url(value) {
    const normalized = value.replace(/-/g, "+").replace(/_/g, "/");
    const padded = normalized.padEnd(normalized.length + (4 - normalized.length % 4) % 4, "=");
    const binary = atob(padded);
    return Uint8Array.from(binary, character => character.charCodeAt(0));
}

async function getDatabase() {
    dbPromise ??= openDatabase();
    return await dbPromise;
}

function openDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(dbName, dbVersion);

        request.onupgradeneeded = event => {
            const database = event.target.result;
            if (!database.objectStoreNames.contains(metaStoreName)) {
                database.createObjectStore(metaStoreName, { keyPath: "key" });
            }
        };

        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error ?? new Error("Opening IndexedDB failed."));
    });
}

function readManifest(database) {
    return readRecord(database, metaStoreName, manifestKey);
}

function readRecord(database, storeName, key) {
    return new Promise((resolve, reject) => {
        const transaction = database.transaction(storeName, "readonly");
        const request = transaction.objectStore(storeName).get(key);

        request.onsuccess = () => resolve(request.result ?? null);
        request.onerror = () => reject(request.error ?? new Error("Reading the local vault failed."));
    });
}

function toBase64Url(value) {
    const binary = String.fromCharCode(...value);
    return btoa(binary)
        .replace(/\+/g, "-")
        .replace(/\//g, "_")
        .replace(/=+$/g, "");
}

function writeRecord(database, storeName, value) {
    return new Promise((resolve, reject) => {
        const transaction = database.transaction(storeName, "readwrite");
        const request = transaction.objectStore(storeName).put(value);

        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error ?? new Error("Writing the local vault failed."));
    });
}
