#!/usr/bin/env bash
# Generate all VHS tape screenshots and GIFs for documentation.
# Requires: vhs (https://github.com/charmbracelet/vhs), ttyd, ffmpeg
#
# Usage:
#   ./docs/tapes/generate.sh           # Generate all tapes
#   ./docs/tapes/generate.sh startup   # Generate a specific tape

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="$SCRIPT_DIR/../images"

mkdir -p "$OUTPUT_DIR"

generate_tape() {
    local tape="$1"
    local name
    name="$(basename "$tape" .tape)"
    echo "🎬 Generating $name..."
    vhs "$tape" -o "$OUTPUT_DIR/$name.gif"
    echo "   ✅ $OUTPUT_DIR/$name.gif"
}

if [ $# -gt 0 ]; then
    # Generate specific tape
    tape="$SCRIPT_DIR/demo-$1.tape"
    if [ -f "$tape" ]; then
        generate_tape "$tape"
    else
        echo "❌ Tape not found: $tape"
        exit 1
    fi
else
    # Generate all tapes
    for tape in "$SCRIPT_DIR"/demo-*.tape; do
        [ -f "$tape" ] || continue
        generate_tape "$tape"
    done
fi

echo ""
echo "📸 All screenshots generated in $OUTPUT_DIR/"
