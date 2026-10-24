#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# Re-shoot the README/website screenshots with demo data and no personal paths.
#
# Standard recipe (also the privacy rule for every public screenshot):
#   - a throwaway HOME, so folder paths render as /tmp/... instead of the real user
#   - demo rows via the app's --shot=<mode> preview flag, never real downloads
#   - a private Xvfb display, so captures are deterministic at 1200x800
#
# Re-run on every release that changes the main window, Settings, or themes.
# Usage: scripts/capture-screenshots.sh [--out docs/screenshots] [--display :99] [--keep]
#   --keep keeps the Xvfb display and throwaway HOME for inspection.
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

OUT="docs/screenshots"
DISPLAY_NUM=":99"
KEEP=0

while [ $# -gt 0 ]; do
    case "$1" in
        --out) OUT="${2:-$OUT}"; shift 2 ;;
        --display) DISPLAY_NUM="${2:-$DISPLAY_NUM}"; shift 2 ;;
        --keep) KEEP=1; shift ;;
        -h|--help) sed -n '2,12p' "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "Unknown option: $1" >&2; exit 2 ;;
    esac
done

log() { echo "[screenshots] $*"; }
die() { echo "[screenshots] ERROR: $*" >&2; exit 1; }
need() { command -v "$1" >/dev/null 2>&1 || die "Need '$1' installed to capture screenshots."; }

need dotnet
need dbus-run-session
need import
need Xvfb
if command -v magick >/dev/null 2>&1; then
    CONVERT="magick"
elif command -v convert >/dev/null 2>&1; then
    CONVERT="convert"
else
    die "Need ImageMagick ('magick' or 'convert') to crop captures."
fi

DLL="app/XDM/XDM.Gtk4.UI/bin/Release/net8.0/fetchflow-gtk4.dll"
FAKEHOME="$(mktemp -d)"
XVFB_PID=""
# Long enough for startup + settle, short enough to never linger
APP_TIMEOUT_SECS=40

cleanup() {
    pkill -f "fetchflow-gtk4.dll" 2>/dev/null || true
    if [ "$KEEP" = 0 ]; then
        [ -n "$XVFB_PID" ] && kill "$XVFB_PID" 2>/dev/null || true
        rm -rf "$FAKEHOME"
    else
        log "Kept display $DISPLAY_NUM (pid $XVFB_PID) and HOME $FAKEHOME."
    fi
}
trap cleanup EXIT

log "Building Release…"
# The log is scanned separately because pipefail would otherwise mask a
# failing build whose output happens to match (or not match) the scan.
build_log="$(dotnet build app/XDM/XDM.Gtk4.UI/XDM.Gtk4.UI.csproj -c Release -v q --nologo 2>&1 || true)"
if printf '%s\n' "$build_log" | grep -qE ": error"; then
    printf '%s\n' "$build_log" | grep -E ": error"
    die "Build failed."
fi
[ -f "$DLL" ] || die "Expected binary missing: $DLL"

log "Starting Xvfb on $DISPLAY_NUM…"
Xvfb "$DISPLAY_NUM" -screen 0 1200x800x24 >/tmp/xvfb-screenshots.log 2>&1 &
XVFB_PID=$!
sleep 3
DISPLAY="$DISPLAY_NUM" import -window root /tmp/shot-probe.png || die "Xvfb on $DISPLAY_NUM is not responding."
rm -f /tmp/shot-probe.png

# Runs one preview mode and imports the screen once it has settled.
# The app has no self-exit in preview mode, so timeout ends the run after the
# capture; exit 124 from timeout is expected, anything else is reported.
capture() {
    local mode="$1" raw="$2" wait_secs="${3:-9}"
    log "Capturing '$mode'…"
    (
        sleep "$wait_secs"; DISPLAY="$DISPLAY_NUM" import -window root "$raw"
    ) &
    local importer=$!
    local code=0
    timeout "$APP_TIMEOUT_SECS"s env HOME="$FAKEHOME" DISPLAY="$DISPLAY_NUM" GDK_BACKEND=x11 FETCHFLOW_SKIP_SINGLE_INSTANCE=1 \
        dbus-run-session -- env dotnet "$DLL" --shot="$mode" >/tmp/shot-"$mode".log 2>&1 || code=$?
    if [ "$code" -ne 0 ] && [ "$code" -ne 124 ]; then
        log "App exited with $code for '$mode' (see /tmp/shot-$mode.log)."
    fi
    wait "$importer" || log "Importer exited early for '$mode'."
    [ -s "$raw" ] || die "Capture failed for '$mode'."
}

capture main /tmp/shot-main.png 9
capture settings /tmp/shot-settings.png 9

mkdir -p "$OUT"
log "Cropping to window geometry…"
"$CONVERT" /tmp/shot-main.png -crop 1042x690+8+0 +repage "$OUT/fetchflow-main-window.png"
"$CONVERT" /tmp/shot-settings.png -crop 906x800+0+0 +repage "$OUT/fetchflow-settings.png"
rm -f /tmp/shot-main.png /tmp/shot-settings.png

log "Done:"
ls -la "$OUT/fetchflow-main-window.png" "$OUT/fetchflow-settings.png"
