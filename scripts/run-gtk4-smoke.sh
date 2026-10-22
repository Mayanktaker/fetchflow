#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# Gtk4Smoke runner — starts an ephemeral Xvfb if needed, runs the GTK 4 smoke test, cleans up.
# Usage:
#   scripts/run-gtk4-smoke.sh                      # headless run (starts Xvfb :99)
#   scripts/run-gtk4-smoke.sh --display :99        # use existing display
#   scripts/run-gtk4-smoke.sh --no-xvfb            # use current WAYLAND_DISPLAY / DISPLAY
#   scripts/run-gtk4-smoke.sh --help
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"

DOTNET="/mnt/Development/_DevSofts/dotnet8/dotnet"
if [ ! -x "$DOTNET" ]; then DOTNET="${HOME}/.dotnet8/dotnet"; fi
if [ ! -x "$DOTNET" ]; then DOTNET="dotnet"; fi
export DOTNET_ROOT="/mnt/Development/_DevSofts/dotnet8"
export PATH="/mnt/Development/_DevSofts/dotnet8:${HOME}/.dotnet8:$PATH"

DISPLAY_NUM=":99"
EXTRA_ARGS=()
OWN_XVFB=1
SCREEN="1024x768x24"

usage() {
  cat <<EOF
run-gtk4-smoke.sh — Xvfb-backed GTK 4 smoke harness for FetchFlow

Exercises the GTK 4 frontend application window, core wiring, and vertical
slice components (NewDownload, Progress, Complete, Alerts) in smoke mode.

Usage:
  $0 [options] [-- <dotnet run args>]

Options:
  --display <:N>   Use existing DISPLAY instead of starting Xvfb (implies --no-xvfb)
  --no-xvfb        Do not start Xvfb; require DISPLAY or WAYLAND_DISPLAY to be set
  --screen WxHxD   Xvfb screen geometry (default: 1024x768x24)
  --help, -h       Show this help

Examples:
  $0
  $0 --no-xvfb
  $0 --display :0
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --help|-h) usage; exit 0 ;;
    --display) DISPLAY_NUM="$2"; OWN_XVFB=0; shift 2 ;;
    --no-xvfb) OWN_XVFB=0; shift ;;
    --screen) SCREEN="$2"; shift 2 ;;
    --) shift; EXTRA_ARGS=("$@"); break ;;
    --*) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
    *) EXTRA_ARGS+=("$1"); shift ;;
  esac
done

XVFB_PID=""
cleanup() {
  if [ -n "${XVFB_PID:-}" ] && kill -0 "$XVFB_PID" 2>/dev/null; then
    kill "$XVFB_PID" 2>/dev/null || true
    wait "$XVFB_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT INT TERM

if [ "$OWN_XVFB" -eq 1 ]; then
  if ! command -v Xvfb >/dev/null 2>&1; then
    if [ -n "${DISPLAY:-}" ] || [ -n "${WAYLAND_DISPLAY:-}" ]; then
      echo "[run-gtk4-smoke] Xvfb not found, but active display detected (DISPLAY=${DISPLAY:-}, WAYLAND_DISPLAY=${WAYLAND_DISPLAY:-}). Falling back to existing display."
      OWN_XVFB=0
    else
      echo "Xvfb not found and no graphical display available. Install Xvfb (e.g. xorg-x11-server-Xvfb) or run under an active desktop session." >&2
      exit 127
    fi
  fi
fi

if [ "$OWN_XVFB" -eq 1 ]; then
  # Pick a free display if default is busy
  if [ -e "/tmp/.X11-unix/X${DISPLAY_NUM#:}" ]; then
    for n in $(seq 99 120); do
      if [ ! -e "/tmp/.X11-unix/X$n" ]; then DISPLAY_NUM=":$n"; break; fi
    done
  fi
  echo "[run-gtk4-smoke] Starting Xvfb $DISPLAY_NUM -screen 0 $SCREEN ..."
  Xvfb "$DISPLAY_NUM" -screen 0 "$SCREEN" >/tmp/xvfb-gtk4smoke.log 2>&1 &
  XVFB_PID=$!
  for _ in $(seq 1 50); do
    [ -e "/tmp/.X11-unix/X${DISPLAY_NUM#:}" ] && break
    sleep 0.1
  done
  if [ ! -e "/tmp/.X11-unix/X${DISPLAY_NUM#:}" ]; then
    echo "[run-gtk4-smoke] Xvfb did not create socket for $DISPLAY_NUM (see /tmp/xvfb-gtk4smoke.log)" >&2
    cat /tmp/xvfb-gtk4smoke.log >&2 || true
    exit 1
  fi
  export DISPLAY="$DISPLAY_NUM"
  # Force X11 backend when using Xvfb
  export GDK_BACKEND="x11"
  unset WAYLAND_DISPLAY || true
  echo "[run-gtk4-smoke] DISPLAY=$DISPLAY (Xvfb pid $XVFB_PID)"
else
  if [ -n "${DISPLAY_NUM:-}" ] && [ "$DISPLAY_NUM" != ":99" ]; then
    export DISPLAY="$DISPLAY_NUM"
  fi
  if [ -z "${DISPLAY:-}" ] && [ -z "${WAYLAND_DISPLAY:-}" ]; then
    echo "[run-gtk4-smoke] --no-xvfb specified but neither DISPLAY nor WAYLAND_DISPLAY is set." >&2
    exit 1
  fi
  echo "[run-gtk4-smoke] Using existing display (DISPLAY=${DISPLAY:-none}, WAYLAND_DISPLAY=${WAYLAND_DISPLAY:-none})"
fi

export FETCHFLOW_SKIP_SINGLE_INSTANCE="1"
echo "[run-gtk4-smoke] Running GTK 4 smoke test..."

if [ ${#EXTRA_ARGS[@]} -gt 0 ]; then
  "$DOTNET" run --project "$REPO_ROOT/app/XDM/XDM.Gtk4.UI/XDM.Gtk4.UI.csproj" "${EXTRA_ARGS[@]}"
else
  "$DOTNET" run --project "$REPO_ROOT/app/XDM/XDM.Gtk4.UI/XDM.Gtk4.UI.csproj" -c Release -- --smoke
fi
