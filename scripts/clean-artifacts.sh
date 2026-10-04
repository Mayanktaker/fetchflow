#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# Purge stale generated packaging trees from app/XDM/XDM.Linux.Installer/.
#
# Why: every make-*-pkg run creates a version-stamped tree (fetchflow-9.1.15.18/,
# fetchflow_9.1.15.18_amd64/, ...). They are never pruned, so the directory grows
# without bound — it reached 3.7 GB across 39 stale trees — and, worse, they poison
# repo-wide searches: a `grep -r '"version"' --include=manifest.json` returns ~60
# hits of which 56 are dead copies, hiding the real manifests.
#
# Safety interlock. This deletes generated output only. For every candidate we
# refuse to act unless ALL of the following hold:
#   1. it matches a known packaging-tree pattern,
#   2. it is matched by .gitignore,
#   3. `git ls-files` reports zero tracked files inside it.
# If a directory is ever checked in, this script refuses to remove it.
#
# Usage:
#   scripts/clean-artifacts.sh            # dry run (prints what it would remove)
#   scripts/clean-artifacts.sh --yes      # actually remove
#   scripts/clean-artifacts.sh --keep-current   # also keep the current version.env tree
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

INSTALLER_DIR="app/XDM/XDM.Linux.Installer"
ASSUME_YES=0
KEEP_CURRENT=0

for arg in "$@"; do
  case "$arg" in
    --yes|-y) ASSUME_YES=1 ;;
    --keep-current) KEEP_CURRENT=1 ;;
    -h|--help) sed -n '2,22p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) echo "unknown argument: $arg" >&2; exit 2 ;;
  esac
done

if [ ! -d "$INSTALLER_DIR" ]; then
  echo "clean-artifacts: $INSTALLER_DIR not found; nothing to do."
  exit 0
fi

cd "$REPO_ROOT"

# Current release version, used to protect the tree a live build just produced.
CURRENT_VERSION=""
if [ -f "$INSTALLER_DIR/version.env" ]; then
  # shellcheck disable=SC1091
  CURRENT_VERSION="$(. "$INSTALLER_DIR/version.env" >/dev/null 2>&1 && printf '%s' "${VERSION:-}")"
fi

# Only version-stamped packaging trees. Deliberately excludes live intermediate
# dirs the packagers read from or write to:
#   src/  binary-source/  pkg/  aur-output/  rpmbuild/
is_candidate() {
  local name="$1"
  case "$name" in
    fetchflow-*|fetchflow_*|xdman_gtk-*|xdman_gtk_*) return 0 ;;
    *) return 1 ;;
  esac
}

removed=0
skipped=0
kept=0

shopt -s nullglob
candidates=("$INSTALLER_DIR"/fetchflow-* "$INSTALLER_DIR"/fetchflow_* \
            "$INSTALLER_DIR"/xdman_gtk-* "$INSTALLER_DIR"/xdman_gtk_*)

for dir in "${candidates[@]}"; do
  [ -d "$dir" ] || continue
  name="$(basename "$dir")"

  if ! is_candidate "$name"; then
    continue
  fi

  # Interlock 3 (checked first: a tracked file also makes check-ignore fail, and
  # "not gitignored" would be the misleading reason to print).
  # NOTE: `sed -n 1p`, never `head -n 1` — head exits after the first line, git takes
  # SIGPIPE, `set -o pipefail` turns that into status 141 and `set -e` kills the whole
  # script partway through the loop (which is how this shipped in the first draft).
  tracked="$(git ls-files -- "$dir" | sed -n '1p')"
  if [ -n "$tracked" ]; then
    echo "SKIP  $name (tracked file: $tracked)"
    skipped=$((skipped + 1))
    continue
  fi

  # Interlock 2: must be gitignored.
  if ! git check-ignore -q "$dir"; then
    echo "SKIP  $name (not gitignored — refusing to touch)"
    skipped=$((skipped + 1))
    continue
  fi

  # Optional: preserve the tree matching the current version.
  if [ "$KEEP_CURRENT" -eq 1 ] && [ -n "$CURRENT_VERSION" ] \
     && [[ "$name" == *"$CURRENT_VERSION"* ]]; then
    echo "KEEP  $name (current version $CURRENT_VERSION)"
    kept=$((kept + 1))
    continue
  fi

  size="$(du -sh "$dir" 2>/dev/null | awk '{print $1}')"
  if [ "$ASSUME_YES" -eq 1 ]; then
    rm -rf -- "$dir"
    echo "REMOVED  $name ($size)"
    removed=$((removed + 1))
  else
    echo "would remove  $name ($size)"
  fi
done

echo
if [ "$ASSUME_YES" -eq 1 ]; then
  echo "clean-artifacts: removed $removed tree(s), kept $kept, skipped $skipped (safety)."
else
  echo "clean-artifacts: dry run. Re-run with --yes to remove."
fi