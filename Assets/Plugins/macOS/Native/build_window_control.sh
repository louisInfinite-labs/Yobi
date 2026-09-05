#!/bin/sh
# Rebuilds YobiWindowControl.bundle from YobiWindowControl.m and drops it next to this
# script's parent folder (Assets/Plugins/macOS), where Unity picks up native macOS plugins.
#
# Builds a universal binary (arm64 + x86_64): Unity's macOS Build Profile can target Intel,
# Apple Silicon, or Universal regardless of which Mac this script runs on, and a thin bundle
# would fail to load on a mismatched target.
set -eu

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT_DIR="$(dirname "$SCRIPT_DIR")"

clang -bundle -fobjc-arc -arch arm64 -arch x86_64 -o "$OUT_DIR/YobiWindowControl.bundle" "$SCRIPT_DIR/YobiWindowControl.m" \
    -framework Cocoa -framework QuartzCore

lipo -info "$OUT_DIR/YobiWindowControl.bundle"

# Ad-hoc signature (`--sign -`) is for local development only - it lets the bundle load
# unblocked by Gatekeeper on this machine. It is NOT accepted by Apple's notary service: a
# release build must re-sign this bundle (and the final .app) with a Developer ID Application
# certificate before distribution/notarization.
codesign --force --sign - "$OUT_DIR/YobiWindowControl.bundle"

echo "Built $OUT_DIR/YobiWindowControl.bundle"
