#!/usr/bin/env bash
# Aimon build + test entry point. Restores, builds (Release), and runs all tests.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
solution="$repo_root/Aimon.slnx"

echo "==> Restoring $solution"
dotnet restore "$solution"

echo "==> Building (Release)"
dotnet build "$solution" -c Release --no-restore

echo "==> Testing"
dotnet test "$solution" -c Release --no-build

echo "==> Done"
