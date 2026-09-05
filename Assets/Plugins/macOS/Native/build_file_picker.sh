#!/bin/sh
# Rebuilds YobiFilePicker.bundle from YobiFilePicker.m and drops it next to this script's
# parent folder (Assets/Plugins/macOS), where Unity picks up native macOS plugins.
#
# Universal binary (arm64 + x86_64): Unity's macOS Build Profile can target Intel, Apple
# Silicon, or Universal regardless of which Mac this script runs on, and a thin bundle would
# fail to load on a mismatched target.
#
# -mmacosx-version-min matches ProjectSettings' macOSTargetOSVersion (12.0) rather than
# whatever Clang would otherwise default to from the build machine's SDK, so the bundle's
# minimum OS requirement can't silently drift from what the Unity Player itself targets.
set -eu

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT_DIR="$(dirname "$SCRIPT_DIR")"

clang -bundle -fobjc-arc -arch arm64 -arch x86_64 -mmacosx-version-min=12.0 -o "$OUT_DIR/YobiFilePicker.bundle" "$SCRIPT_DIR/YobiFilePicker.m" \
    -framework Cocoa -framework UniformTypeIdentifiers

lipo -info "$OUT_DIR/YobiFilePicker.bundle"

# Ad-hoc signature (`--sign -`) is for local development only - a release build must re-sign
# this bundle (and the final .app) with a Developer ID Application certificate before
# distribution/notarization.
codesign --force --sign - "$OUT_DIR/YobiFilePicker.bundle"

echo "Built $OUT_DIR/YobiFilePicker.bundle"
