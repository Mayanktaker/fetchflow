#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
set -e

# Resolve paths
BASE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHROME_EXT_DIR="$HOME/Downloads/fetchflow-chrome-extension"
FIREFOX_EXT_DIR="$HOME/Downloads/fetchflow-firefox-extension"

mkdir -p "$CHROME_EXT_DIR" "$FIREFOX_EXT_DIR"

# Copy latest unpacked extension sources
if [ -d "$BASE_DIR/app/XDM/chrome-extension" ]; then
    cp -r "$BASE_DIR/app/XDM/chrome-extension/"* "$CHROME_EXT_DIR/"
fi

if [ -d "$BASE_DIR/app/XDM/firefox-amo" ]; then
    cp -r "$BASE_DIR/app/XDM/firefox-amo/"* "$FIREFOX_EXT_DIR/"
fi

chmod +x "$BASE_DIR/scripts/install-extensions.sh" 2>/dev/null || :

echo "======================================================="
echo "  FetchFlow Download Manager — Browser Integration Helper"
echo "======================================================="
echo ""
echo "Extension directories ready:"
echo "  • Chrome/Chromium: $CHROME_EXT_DIR"
echo "  • Firefox:         $FIREFOX_EXT_DIR"
echo ""
echo "--- Installation Instructions ---"
echo "1. Firefox:"
echo "   - Open Firefox and navigate to: about:debugging#/runtime/this-firefox"
echo "   - Click 'Load Temporary Add-on...'"
echo "   - Select: $FIREFOX_EXT_DIR/manifest.json"
echo ""
echo "2. Chrome / Brave / Edge / Chromium:"
echo "   - Navigate to: chrome://extensions/"
echo "   - Turn ON 'Developer mode' (top right)"
echo "   - Click 'Load unpacked'"
echo "   - Select folder: $CHROME_EXT_DIR"
echo ""
echo "======================================================="
