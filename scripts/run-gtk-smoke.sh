#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# GtkSmoke runner — starts an ephemeral Xvfb, runs the GtkSmoke test under DISPLAY, cleans up.
# Usage:
#   scripts/run-gtk-smoke.sh                      # headless run (starts Xvfb :99)
#   scripts/run-gtk-smoke.sh --display :99        # use existing display
#   scripts/run-gtk-smoke.sh --help
#   # Manual equivalent:
#   #   Xvfb :99 -screen 0 1024x768x24 &
#   #   DISPLAY=:99 dotnet test --filter GtkSmoke
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
DOTNET="${HOME}/.dotnet8/dotnet"
if [ ! -x "$DOTNET" ]; then DOTNET="dotnet"; fi
export DOTNET_ROOT="${HOME}/.dotnet8"
export PATH="${HOME}/.dotnet8:$PATH"

DISPLAY_NUM=":99"
EXTRA_ARGS=()
OWN_XVFB=1
SCREEN="1024x768x24"

usage() {
  cat <<EOF
run-gtk-smoke.sh — Xvfb-backed GtkSmoke harness for FetchFlow

Runs the GtkSmoke test category (real Gtk.Builder load + Autoconnect) under a
virtual display so CI without a desktop can still exercise GTK wiring.

Usage:
  $0 [options] [-- <dotnet test args>]

Options:
  --display <:N>   Use existing DISPLAY instead of starting Xvfb (implies --no-xvfb)
  --no-xvfb        Do not start Xvfb; require DISPLAY to be set
  --screen WxHxD   Xvfb screen geometry (default: 1024x768x24)
  --help, -h       Show this help

Examples:
  $0
  $0 -- dotnet test -c Debug --filter GtkSmoke
  DISPLAY=:0 $0 --no-xvfb
  $0 --display :99 -- -c Release --filter GtkSmoke

Environment:
  DOTNET            dotnet binary (default: ~/.dotnet8/dotnet, fallback: dotnet on PATH)
  DISPLAY           respected when --no-xvfb / --display is used

Notes:
  - Without Xvfb and without DISPLAY, the GtkSmoke test is Skipped/Inconclusive
    so "dotnet test" still passes; GladeWiringTests already covers id drift headless.
  - Filter tag: [TestCategory("GtkSmoke")] / dotnet test --filter "TestCategory=GtkSmoke"
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
    echo "Xvfb not found. Install it (Fedora: sudo dnf install -y xorg-x11-server-Xvfb) or run with --no-xvfb and a real DISPLAY." >&2
    exit 127
  fi
  # Pick a free display if :99 is busy
  if [ -e "/tmp/.X11-unix/X${DISPLAY_NUM#:}" ]; then
    for n in $(seq 99 120); do
      if [ ! -e "/tmp/.X11-unix/X$n" ]; then DISPLAY_NUM=":$n"; break; fi
    done
  fi
  echo "[run-gtk-smoke] Starting Xvfb $DISPLAY_NUM -screen 0 $SCREEN ..."
  Xvfb "$DISPLAY_NUM" -screen 0 "$SCREEN" >/tmp/xvfb-gtksmoke.log 2>&1 &
  XVFB_PID=$!
  # Wait for socket
  for _ in $(seq 1 50); do
    [ -e "/tmp/.X11-unix/X${DISPLAY_NUM#:}" ] && break
    sleep 0.1
  done
  if [ ! -e "/tmp/.X11-unix/X${DISPLAY_NUM#:}" ]; then
    echo "[run-gtk-smoke] Xvfb did not create socket for $DISPLAY_NUM (see /tmp/xvfb-gtksmoke.log)" >&2
    cat /tmp/xvfb-gtksmoke.log >&2 || true
    exit 1
  fi
  export DISPLAY="$DISPLAY_NUM"
  echo "[run-gtk-smoke] DISPLAY=$DISPLAY (Xvfb pid $XVFB_PID)"
else
  if [ -n "${DISPLAY_NUM:-}" ] && [ "$DISPLAY_NUM" != ":99" ]; then
    export DISPLAY="$DISPLAY_NUM"
  fi
  if [ -z "${DISPLAY:-}" ]; then
    echo "[run-gtk-smoke] --no-xvfb but no DISPLAY set — GtkSmoke will be Skipped (Inconclusive)." >&2
  else
    echo "[run-gtk-smoke] Using existing DISPLAY=$DISPLAY (no Xvfb)"
  fi
fi

set -x
if [ ${#EXTRA_ARGS[@]} -gt 0 ]; then
  "$DOTNET" "${EXTRA_ARGS[@]}"
else
  "$DOTNET" test "$REPO_ROOT/app/XDM/XDM.Tests/XDM.Tests.csproj" -c Debug --filter "TestCategory=GtkSmoke" --logger "console;verbosity=detailed"
fi
