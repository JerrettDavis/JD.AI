#!/usr/bin/env bash
set -euo pipefail

# ── Smart Restage ─────────────────────────────────────────────
# Only restages files that were ACTUALLY modified by the formatting
# hooks (jb-cleanup, dotnet-format). Prevents blindly overwriting
# staged content when formatters don't change a file.
#
# Without this, `git add ${staged}` would restage ALL originally
# staged files from the working directory — including files that
# formatters reverted to their pre-edit state.

STAGED_FILES=$(git diff --cached --name-only --diff-filter=ACMR)

if [ -z "$STAGED_FILES" ]; then
  exit 0
fi

RESTAGED=0
for file in $STAGED_FILES; do
  # Only restage .cs files (formatters only touch C#)
  case "$file" in
    *.cs) ;;
    *) continue ;;
  esac

  # Check if the working-tree version differs from the staged version.
  # If the formatter modified the file, the working tree will differ
  # from the index — we need to restage it.
  if ! git diff --quiet -- "$file" 2>/dev/null; then
    git add "$file"
    RESTAGED=$((RESTAGED + 1))
  fi
done

if [ $RESTAGED -gt 0 ]; then
  echo "Restaged $RESTAGED file(s) modified by formatters."
fi
