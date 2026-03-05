#!/usr/bin/env bash
set -euo pipefail

# ── JetBrains CleanupCode Wrapper ──────────────────────────────
# Wraps jb cleanupcode to handle exit code 3 (no items found)
# as success, since that's expected when no .cs files are staged.

FILES="$*"

if [ -z "$FILES" ]; then
  echo "No files to clean up — skipping."
  exit 0
fi

dotnet jb cleanupcode JD.AI.slnx \
  --profile="Built-in: Full Cleanup" \
  --include="$FILES" \
  --no-build
rc=$?

# Exit code 3 = "no items found to cleanup" — treat as success
if [ $rc -eq 3 ]; then
  exit 0
fi

exit $rc
