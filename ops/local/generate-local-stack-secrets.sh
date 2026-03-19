#!/usr/bin/env bash

set -euo pipefail

force="${1:-}"

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
secret_dir="${script_dir}/secrets"
cert_dir="${repo_root}/ops/vernemq/certs"
extfile="${script_dir}/vernemq-cert.ext"

jwt_key_path="${secret_dir}/realtime-signing-private-key.pem"
ca_key_path="${cert_dir}/ca.key"
ca_cert_path="${cert_dir}/ca.pem"
server_key_path="${cert_dir}/tls.key"
server_csr_path="${cert_dir}/tls.csr"
server_cert_path="${cert_dir}/tls.crt"
ca_serial_path="${cert_dir}/ca.srl"

mkdir -p "${secret_dir}" "${cert_dir}"

should_skip() {
    local path="$1"
    [[ "${force}" != "--force" && -f "${path}" ]]
}

if should_skip "${jwt_key_path}"; then
    printf 'Skipping existing JWT signing key: %s\n' "${jwt_key_path}"
else
    openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "${jwt_key_path}"
    printf 'Generated JWT signing key: %s\n' "${jwt_key_path}"
fi

if should_skip "${ca_cert_path}" && should_skip "${ca_key_path}" && should_skip "${server_cert_path}" && should_skip "${server_key_path}"; then
    printf 'Skipping existing VerneMQ TLS materials in %s\n' "${cert_dir}"
else
    openssl genrsa -out "${ca_key_path}" 4096
    openssl req \
        -x509 \
        -new \
        -nodes \
        -key "${ca_key_path}" \
        -sha256 \
        -days 3650 \
        -out "${ca_cert_path}" \
        -subj "/CN=MeshBoard Local Dev CA"

    openssl genrsa -out "${server_key_path}" 2048
    openssl req \
        -new \
        -key "${server_key_path}" \
        -out "${server_csr_path}" \
        -subj "/CN=localhost"
    openssl x509 \
        -req \
        -in "${server_csr_path}" \
        -CA "${ca_cert_path}" \
        -CAkey "${ca_key_path}" \
        -CAcreateserial \
        -out "${server_cert_path}" \
        -days 825 \
        -sha256 \
        -extfile "${extfile}"

    rm -f "${server_csr_path}" "${ca_serial_path}"

    printf 'Generated VerneMQ TLS materials in %s\n' "${cert_dir}"
fi

printf '\nTrust the CA certificate for browser or local WSS testing:\n'
printf '  %s\n' "${ca_cert_path}"
