#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# Relaunch a dev build of either frontend on the current session for live testing.
#
# Why setsid: `nohup … &` only ignores SIGHUP. When the launching shell's process
# group is torn down, the app dies with it — that is exactly what happened to a
# GTK 3 launch that looked successful and then vanished. setsid detaches the app
# into its own session so it survives the shell that started it.
#
# Stops are comm-guarded: a bare `pkill -f <pattern>` once killed the invoking
# shell itself, because the shell's own command line matches the pattern.
# Usage: scripts/relaunch-app.sh [--gtk4|--gtk3] [--no-build]
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

log() { echo "[relaunch] $*"; }
die() { echo "[relaunch] ERROR: $*" >&2; exit 1; }

FRONTEND="gtk4"
SKIP_BUILD=0
while [ $# -gt 0 ]; do
    case "$1" in
        --gtk4) FRONTEND="gtk4"; shift ;;
        --gtk3) FRONTEND="gtk3"; shift ;;
        --no-build) SKIP_BUILD=1; shift ;;
        -h|--help) sed -n '2,13p' "${BASH_SOURCE[0]}"; exit 0 ;;
        *) die "Unknown option: $1" ;;
    esac
done

if [ "$FRONTEND" = "gtk3" ]; then
    PROJECT="app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj"
    APP_DIR="app/XDM/XDM.Gtk.UI/bin/Release/net8.0"
    APP_DLL="fetchflow.dll"
    # Matches both arg forms (`dotnet fetchflow.dll`, full path) but not the
    # installed /opt binary, which runs natively and never has comm=dotnet.
    # Does not match fetchflow-gtk4.dll thanks to the literal ".dll".
    PATTERN="fetchflow\.dll"
    LOG="/tmp/fetchflow-gtk3-dev.log"
else
    PROJECT="app/XDM/XDM.Gtk4.UI/XDM.Gtk4.UI.csproj"
    APP_DIR="app/XDM/XDM.Gtk4.UI/bin/Release/net8.0"
    APP_DLL="fetchflow-gtk4.dll"
    PATTERN="fetchflow-gtk4.dll"
    LOG="/tmp/fetchflow-gtk4-dev.log"
fi

if [ "$SKIP_BUILD" = 0 ]; then
    log "Building $FRONTEND Release…"
    build_log="$(dotnet build "$PROJECT" -c Release -v q --nologo 2>&1 || true)"
    if printf '%s\n' "$build_log" | grep -qE ": error"; then
        printf '%s\n' "$build_log" | grep -E ": error"
        die "Build failed."
    fi
fi
[ -f "$APP_DIR/$APP_DLL" ] || die "Expected binary missing: $APP_DIR/$APP_DLL"

log "Stopping previous $FRONTEND dev instances…"
# shellcheck disable=SC2009
for pid in $(pgrep -f "$PATTERN" 2>/dev/null || true); do
    if [ "$(ps -p "$pid" -o comm= 2>/dev/null)" = "dotnet" ]; then
        log "Stopping pid $pid."
        kill "$pid" 2>/dev/null || true
    fi
done
sleep 2

if ss -ltn 2>/dev/null | grep -q ":8597 "; then
    die "Port 8597 is still held (an installed /opt build, or another frontend?). Stop it first."
fi

if [ -z "${DOTNET_ROOT:-}" ] && [ -d /mnt/Development/_DevSofts/dotnet8 ]; then
    export DOTNET_ROOT=/mnt/Development/_DevSofts/dotnet8
fi
unset GDK_BACKEND DISPLAY || true

log "Launching $FRONTEND…"
cd "$APP_DIR"
setsid nohup dotnet "$APP_DLL" > "$LOG" 2>&1 < /dev/null &
cd "$REPO_ROOT"
sleep 10

pid="$(pgrep -f "$PATTERN" 2>/dev/null | head -n1 || true)"
[ -n "$pid" ] || die "App is not running after launch. Tail of $LOG: $(tail -5 "$LOG")"
if ! ss -ltn 2>/dev/null | grep -q ":8597 "; then
    die "No IPC listener after launch. Tail of $LOG: $(tail -5 "$LOG")"
fi
errors="$(grep -ciE 'Adwaita-ERROR|unhandled exception|Gtk-CRITICAL|theme swap failed' "$LOG" || true)"
log "$FRONTEND up (pid $pid), IPC listening on 8597, $errors error(s) in log."
