#!/bin/sh
# Rebuilds YobiWindowControl.bundle from YobiWindowControl.m and drops it next to this
# script's parent folder (Assets/Plugins/macOS), where Unity picks up native macOS plugins.
#
# Only builds for the architecture of the machine running this script. Before distributing a
# build to other Macs, rebuild as a universal binary instead:
#   clang -arch arm64 -arch x86_64 -bundle ...
set -eu

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT_DIR="$(dirname "$SCRIPT_DIR")"

clang -bundle -fobjc-arc -o "$OUT_DIR/YobiWindowControl.bundle" "$SCRIPT_DIR/YobiWindowControl.m" \
    -framework Cocoa -framework QuartzCore

codesign --force --sign - "$OUT_DIR/YobiWindowControl.bundle"

echo "Built $OUT_DIR/YobiWindowControl.bundle"
