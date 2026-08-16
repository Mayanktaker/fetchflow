#!/bin/bash
# © 2026 Mayanktaker | Based on XDM by subhra74 (https://github.com/subhra74/xdm)
set -e

# Default repository
REPO="Mayanktaker/xdm"

echo "Checking for the latest release on GitHub ($REPO)..."
LATEST_URL=$(curl -s "https://api.github.com/repos/$REPO/releases/latest" | grep "browser_download_url" | grep "xdm-app" | cut -d '"' -f 4)

if [ -z "$LATEST_URL" ]; then
    # Fallback to downloading a generic tar.gz
    LATEST_URL=$(curl -s "https://api.github.com/repos/$REPO/releases/latest" | grep "browser_download_url" | grep ".tar.gz" | head -n 1 | cut -d '"' -f 4)
fi

if [ -z "$LATEST_URL" ]; then
    echo "Could not find a valid download URL (xdm-app or .tar.gz) for the latest release."
    exit 1
fi

echo "Found update: $LATEST_URL"
echo "Downloading update..."

TMP_DIR=$(mktemp -d)
cd "$TMP_DIR"

curl -L -o update_package "$LATEST_URL"

echo "Terminating any running instances of XDM..."
killall xdm-app 2>/dev/null || true

echo "Applying update (requires sudo to write to /opt/xdman)..."
if [[ "$LATEST_URL" == *".tar.gz"* ]]; then
    tar -xzf update_package
    sudo cp -f xdm-app /opt/xdman/xdm-app
else
    # It's the raw binary
    sudo cp -f update_package /opt/xdman/xdm-app
fi

sudo chmod 755 /opt/xdman/xdm-app

echo "Update applied successfully!"
echo "Cleaning up..."
cd /
rm -rf "$TMP_DIR"

echo "Starting XDM..."
# Launch via background
export GTK_USE_PORTAL=1
nohup /opt/xdman/xdm-app >/dev/null 2>&1 &

echo "Update complete."
