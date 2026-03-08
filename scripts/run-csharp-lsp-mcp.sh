#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

if ! command -v dnx >/dev/null 2>&1; then
  echo "dnx was not found. Install .NET SDK 10 or newer." >&2
  exit 1
fi

# Expose the local csharp-ls wrapper so csharp-lsp-mcp can resolve it.
PATH="$SCRIPT_DIR:$PATH"
export PATH

exec dnx CSharpLspMcp@1.0.0 --yes -- "$@"
