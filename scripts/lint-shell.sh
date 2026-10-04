#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# Shell lint gate — one file list and one severity, shared by local runs and CI.
# lint.yml calls this script, so adding a script here is the whole change.
# Usage: scripts/lint-shell.sh [extra shellcheck args]
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

# SHELLCHECK env wins, then PATH, then the user-local install (no sudo needed).
find_shellcheck() {
  if [ -n "${SHELLCHECK:-}" ] && [ -x "${SHELLCHECK}" ]; then
    printf '%s' "$SHELLCHECK"
    return 0
  fi
  if command -v shellcheck >/dev/null 2>&1; then
    command -v shellcheck
    return 0
  fi
  local user_tool="${HOME}/.local/tools/shellcheck"
  if [ -x "$user_tool" ]; then
    printf '%s' "$user_tool"
    return 0
  fi
  return 1
}

if ! SHELLCHECK_BIN="$(find_shellcheck)"; then
  echo "shellcheck not found." >&2
  echo "  CI installs it with: sudo apt-get install -y shellcheck" >&2
  echo "  Locally you can set: SHELLCHECK=/path/to/shellcheck" >&2
  exit 127
fi

# Everything shellcheck must cover: every repo script, the root helpers, and the
# three package builders. Globs expand at assignment time, so scripts/*.sh is a
# real file list — self-including for this file.
TARGETS=(
  scripts/*.sh
  build_all.sh
  build_rpm.sh
  xdm-updater.sh
  app/XDM/XDM.Linux.Installer/make-arch-pkg
  app/XDM/XDM.Linux.Installer/make-deb-pkg
  app/XDM/XDM.Linux.Installer/make-rpm-pkg
)

printf 'shellcheck %s (%s)\n' \
  "$("$SHELLCHECK_BIN" --version | awk '/^version:/ {print $2}')" \
  "$SHELLCHECK_BIN"
printf 'targets   : %d files\n' "${#TARGETS[@]}"

# -S warning matches the CI gate: informational notes never fail the build.
"$SHELLCHECK_BIN" -S warning "${TARGETS[@]}" "$@"

echo "shellcheck: OK"
