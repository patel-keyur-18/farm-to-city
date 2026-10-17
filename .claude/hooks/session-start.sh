#!/bin/bash
# SessionStart hook (US-003, ADR 0019): makes a fresh cloud session able to
# run `dotnet build` and `dotnet test` without manual setup.
set -euo pipefail

# Only cloud sessions need this; local machines manage their own SDK.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# Install the .NET 10 SDK (ADR 0026) when it's missing.
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  echo "Installing dotnet-sdk-10.0..."
  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0
fi

dotnet --version

# Restore NuGet packages so the first build is fast and works offline.
cd "${CLAUDE_PROJECT_DIR:-$(dirname "$0")/../..}"
if [ -f FarmToCity.sln ]; then
  dotnet restore FarmToCity.sln --verbosity quiet
fi
