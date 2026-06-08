#!/usr/bin/env bash
# Builds the Teams app package (appPackage.zip) from this folder.
# The three files MUST sit at the ZIP root - no parent folder.
set -euo pipefail
cd "$(dirname "$0")"

for f in manifest.json color.png outline.png; do
  [ -f "$f" ] || { echo "Missing $f (run ./generate-icons.py for placeholder icons)"; exit 1; }
done

rm -f appPackage.zip
zip -j appPackage.zip manifest.json color.png outline.png >/dev/null
echo "Built $(pwd)/appPackage.zip"
unzip -l appPackage.zip
