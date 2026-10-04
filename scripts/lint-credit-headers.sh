#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# Credit-header gate — Rule 5 enforcement on changed files only.
#
# Rule 5 says every file carries the credit header, but ~70% of the tracked tree is
# inherited upstream XDM code that never had one. Failing CI on the whole tree would
# be unfixable without a 372-file diff that also mis-attributes upstream code, so the
# rule is enforced prospectively: any file you MODIFY must carry the header. Existing
# files are grandfathered and stay that way until someone edits them.
#
# The gate therefore diffs against the merge base, not the whole tree.
#
# Usage:
#   scripts/lint-credit-headers.sh                 # diff vs origin/myfork default branch
#   scripts/lint-credit-headers.sh --base main     # explicit base ref
#   scripts/lint-credit-headers.sh --staged        # only what is staged right now
#   scripts/lint-credit-headers.sh --all           # audit the whole tree, report only
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

MODE="diff"
BASE=""

while [ $# -gt 0 ]; do
  case "$1" in
    --staged) MODE="staged" ;;
    --all) MODE="all" ;;
    --base) MODE="diff"; shift; BASE="${1:-}" ;;
    -h|--help) sed -n '2,17p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done

# Extensions the rule covers: source, markup, styling, scripts.
# Compared as BARE EXTENSION NAMES, not as "*.ext" globs. Glob patterns here would
# either need a shellcheck suppression (an unquoted RHS warns as accidental) or a
# quoted RHS that silently matches nothing. A plain name list is unambiguous.
EXTENSIONS=( cs xaml js mjs sh css html glade )

MARKER="mayanktaker.com"

# One header line is enough; we do not police its exact wording.
has_header() {
  head -n 8 "$1" 2>/dev/null | grep -qi "$MARKER"
}

# Collect candidate paths, honouring the mode.
collect() {
  case "$MODE" in
    staged) git diff --cached --name-only --diff-filter=ACMR ;;
    all)    git ls-files ;;
    diff)
      local base="$BASE"
      if [ -z "$base" ]; then
        # Prefer an upstream-tracking ref; fall back to HEAD~1 in a shallow/one-commit repo.
        base="$(git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null || true)"
        [ -n "$base" ] || base="$(git symbolic-ref --short refs/remotes/origin/HEAD 2>/dev/null | sed 's|^origin/||' || true)"
        [ -n "$base" ] || base="HEAD"
      fi
      # Two-dot diff of the working tree against the base; merge-base keeps feature
      # branches honest when the base has moved on.
      local mb
      mb="$(git merge-base "$base" HEAD 2>/dev/null || echo "$base")"
      { git diff --name-only --diff-filter=ACMR "$mb" --; git diff --cached --name-only --diff-filter=ACMR --; } | sort -u
      ;;
  esac
}

matched_list() {
  printf '%s\n' "${EXTENSIONS[@]}"
}

violations=0
checked=0

while IFS= read -r file; do
  [ -n "$file" ] || continue
  [ -f "$file" ] || continue

  # Extension filter: keep the file only if its suffix is one we cover.
  # ${file##*.} on a dotless name yields the whole name, which matches nothing here,
  # so extensionless files (.gitignore, LICENSE) are correctly ignored.
  file_ext="${file##*.}"
  covered=0
  for ext in "${EXTENSIONS[@]}"; do
    if [ "$file_ext" = "$ext" ]; then covered=1; break; fi
  done
  [ "$covered" -eq 1 ] || continue

  checked=$((checked + 1))
  if ! has_header "$file"; then
    echo "MISSING credit header: $file"
    echo "    add: // © Mayanktaker Computers & Web Development | https://mayanktaker.com"
    violations=$((violations + 1))
  fi
done < <(collect)

echo
printf 'credit-headers: checked %d file(s), %d violation(s)\n' "$checked" "$violations"

if [ "$violations" -gt 0 ]; then
  if [ "$MODE" = "all" ]; then
    echo "(report-only mode: --all never fails)"
    exit 0
  fi
  echo "Rule 5: every file you modify must carry the credit header."
  exit 1
fi

echo "credit-headers: OK"