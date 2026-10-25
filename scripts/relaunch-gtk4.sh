#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# Compatibility wrapper: relaunch the GTK 4 dev build.
# The engine lives in relaunch-app.sh (setsid launch, comm-guarded stop, IPC check).
# Usage: scripts/relaunch-gtk4.sh [--no-build]
set -euo pipefail
exec "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/relaunch-app.sh" --gtk4 "$@"
