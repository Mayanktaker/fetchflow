#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# Relaunch the GTK 4 dev build on the current session for live testing.
#
# Safe restart pattern (a bare `pkill -f <pattern>` once killed the invoking
# shell itself, because the shell's own command line matches): every candidate
# pid from pgrep is re-checked with `ps -o comm=` before it is signalled.
# Usage: scripts/relaunch-gtk4.sh [--no-build]
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

APP_DIR="app/XDM/XDM.Gtk4.UI/bin/Release/net8.0"
APP_DLL="$APP_DIR/fetchflow-gtk4.dll"
LOG="/tmp/fetchflow-gtk4-dev.log"
PATTERN="fetchflow-gtk4.dll"

log() { echo "[relaunch] $*"; }
die() { echo "[relaunch] ERROR: $*" >&2; exit 1; }

SKIP_BUILD=0
[ "${1:-}" = "--no-build" ] && SKIP_BUILD=1

if [ "$SKIP_BUILD" = 0 ]; then
    log "Building Release…"
    build_log="$(dotnet build app/XDM/XDM.Gtk4.UI/XDM.Gtk4.UI.csproj -c Release -v q --nologo 2>&1 || true)"
    if printf '%s\n' "$build_log" | grep -qE ": error"; then
        printf '%s\n' "$build_log" | grep -E ": error"
        die "Build failed."
    fi
fi
[ -f "$APP_DLL" ] || die "Expected binary missing: $APP_DLL"

# Stop previous dev instances. The comm guard is the whole point: pgrep -f
# matches our own shell too (its command line contains the pattern), so only
# signal pids whose process name is the dotnet host.
log "Stopping previous dev instances…"
# shellcheck disable=SC2009
for pid in $(pgrep -f "dotnet $PATTERN" 2>/dev/null || true); do
    if [ "$(ps -p "$pid" -o comm= 2>/dev/null)" = "dotnet" ]; then
        log "Stopping pid $pid."
        kill "$pid" 2>/dev/null || true
    fi
done
sleep 2

if ss -ltn 2>/dev/null | grep -q ":8597 "; then
    die "Port 8597 is still held (the installed /opt build?). Stop it first, then re-run."
fi

if [ -z "${DOTNET_ROOT:-}" ] && [ -d /mnt/Development/_DevSofts/dotnet8 ]; then
    export DOTNET_ROOT=/mnt/Development/_DevSofts/dotnet8
fi
unset GDK_BACKEND DISPLAY || true

log "Launching…"
cd "$APP_DIR"
# shellcheck disable=SC2086
nohup dotnet fetchflow-gtk4.dll > "$LOG" 2>&1 &
sleep 8

if ! ss -ltn 2>/dev/null | grep -q ":8597 "; then
    die "No IPC listener after launch. Tail of $LOG: $(tail -5 "$LOG")"
fi
errors="$(grep -ciE 'Adwaita-ERROR|unhandled exception|theme swap failed' "$LOG" || true)"
log "Up, IPC listening on 8597, $errors error(s) in log."
