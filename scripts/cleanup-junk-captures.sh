#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# One-shot purge of junk auto-captures (autocomplete, telemetry, SW, sticker CDNs).
# Usage: quit FetchFlow first, then: scripts/cleanup-junk-captures.sh [path-to-downloads.db]
set -euo pipefail

# Refuse while the app is live — SQLite writes under a running instance risk locks
if pgrep -f "/opt/fetchflow/fetchflow" >/dev/null 2>&1 || pgrep -x "fetchflow" >/dev/null 2>&1; then
    echo "ERROR: FetchFlow is running. Quit it first, then re-run this script." >&2
    exit 1
fi
# sqlite3 CLI is required for the DB purge
command -v sqlite3 >/dev/null 2>&1 || { echo "ERROR: sqlite3 not found." >&2; exit 1; }

DB="${1:-$HOME/.fetchflow-app-data/downloads.db}"
[ -f "$DB" ] || { echo "ERROR: DB not found: $DB" >&2; exit 1; }
# Single predicate mirroring NetworkHelper.IsNoiseUrl + both noise-filter.js twins
PRED="primary_url LIKE '%complete/search%' OR primary_url LIKE '%/complete/s%' OR primary_url LIKE '%google.com/async/%' OR primary_url LIKE '%google.com/httpservice/%' OR primary_url LIKE '%getdatasyncids%' OR primary_url LIKE '%sw.js%' OR primary_url LIKE '%/api/timedtext%' OR primary_url LIKE '%gen_204%' OR primary_url LIKE '%/api/stats%' OR primary_url LIKE '%google.com/log%' OR primary_url LIKE '%safebrowsing%' OR primary_url LIKE '%fbsbx.com%' OR primary_url LIKE '%_next/image%'"

BEFORE=$(sqlite3 "$DB" "SELECT COUNT(*) FROM downloads WHERE $PRED;")
echo "Junk rows found: $BEFORE"
[ "$BEFORE" -eq 0 ] && { echo "Nothing to clean."; exit 0; }
# Filenames of the junk rows, resolved before the DELETE below
MAPFILE -t NAMES < <(sqlite3 "$DB" "SELECT name FROM downloads WHERE $PRED;")
# Backup first: DB copy + moved files land here, restore = copy back
BACKUP="$HOME/.fetchflow-app-data/cleanup-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP/files"
cp "$DB" "$BACKUP/downloads.db.bak"
echo "Backup: $BACKUP"

sqlite3 "$DB" "DELETE FROM downloads WHERE $PRED;"
echo "Rows after purge: $(sqlite3 "$DB" 'SELECT COUNT(*) FROM downloads;')"
# Move each junk file out of the download folders (DB rows are already gone)
MOVED=0
for n in "${NAMES[@]}"; do
    while IFS= read -r f; do
        mv "$f" "$BACKUP/files/" && MOVED=$((MOVED + 1))
    done < <(find "$HOME/Downloads/FetchFlow" -maxdepth 2 -type f -name "$n" 2>/dev/null)
done
echo "Files moved to backup: $MOVED"
echo "Done. Restore with: cp $BACKUP/downloads.db.bak \"$DB\""
