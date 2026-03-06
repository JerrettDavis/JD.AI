#!/usr/bin/env bash
set -euo pipefail

# ── JetBrains CleanupCode Wrapper ──────────────────────────────
# Wraps jb cleanupcode to handle exit code 3 (no items found)
# as success, since that's expected when no .cs files are staged.
#
# Uses the custom "Pre-commit" profile (defined in .DotSettings)
# which only applies formatting + using optimization.
# Does NOT apply code transformations (var style, brace removal,
# naming conventions, etc.) to prevent silently reverting semantic
# changes during git hooks.

FILES="$*"

if [ -z "$FILES" ]; then
  echo "No files to clean up — skipping."
  exit 0
fi

dotnet jb cleanupcode JD.AI.slnx \
  --profile="Pre-commit" \
  --include="$FILES" \
  --no-build \
  || rc=$?
rc=${rc:-0}

# Exit code 3 = "no items found to cleanup" — treat as success
if [ $rc -eq 3 ]; then
  exit 0
fi

exit $rc
