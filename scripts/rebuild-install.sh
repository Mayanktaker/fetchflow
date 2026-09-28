#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
#
# rebuild-install.sh — build FetchFlow from source and install it with pacman.
#
# Use this for local development installs on Arch-family systems (Manjaro, CachyOS, Arch).
# It does NOT publish anything: use build_all.sh for a real release.
#
#   1. Runs the automated test suite (build gate — aborts on failure)
#   2. Publishes the self-contained single-file binary
#   3. Stages runtime assets (glade, svg-icons, extensions, Lang, brand icons)
#   4. Builds fetchflow-<version>-<rel>-x86_64.pkg.tar.zst
#   5. Installs it with pacman -U
#   6. Repairs the launch-at-login entry and verifies the result
#
# Usage:
#   scripts/rebuild-install.sh              # build + install
#   scripts/rebuild-install.sh --no-install # build only (no sudo, no pacman)
#   scripts/rebuild-install.sh --yes        # skip the confirmation prompt
#
set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
INSTALLER_DIR="${REPO_ROOT}/app/XDM/XDM.Linux.Installer"
PUBLISH_DIR="${REPO_ROOT}/build_output/xdm-app"
DOTNET_DIR="${HOME}/.dotnet8"
GTK_PROJECT="app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj"
TEST_RUNNER="app/XDM/XDM.Tests/bin/Release/net8.0/XDM.Tests.dll"
BRAND_SVG="app/XDM/fetchflow-logo.svg"
TRAY_SVG="svg-icons/fetchflow-tray.svg"

DO_INSTALL=1
ASSUME_YES=0

# © Mayanktaker Computers & Web Development | https://mayanktaker.com
log()  { printf '\n\033[1;36m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[!]\033[0m %s\n' "$*"; }
die()  { printf '\033[1;31m[x]\033[0m %s\n' "$*" >&2; exit 1; }

for arg in "$@"; do
    case "$arg" in
        --no-install) DO_INSTALL=0 ;;
        --yes|-y)    ASSUME_YES=1 ;;
        -h|--help)   sed -n '2,20p' "$0"; exit 0 ;;
        *)           die "Unknown option: $arg (try --help)" ;;
    esac
done

# Prefer the pinned SDK so the publish uses the same toolchain as the release build.
if [ -d "$DOTNET_DIR" ]; then
    export PATH="${DOTNET_DIR}:${PATH}"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
command -v dotnet >/dev/null || die "dotnet not found (expected ${DOTNET_DIR}/dotnet)"

cd "$REPO_ROOT"

# --- 0. Preflight -----------------------------------------------------------
log "Preflight"

# The app holds the single-file bundle open; stop it before we overwrite it.
if pgrep -x fetchflow >/dev/null 2>&1; then
    for pid in $(pgrep -x fetchflow); do kill "$pid" 2>/dev/null || true; done
    sleep 2
    if pgrep -x fetchflow >/dev/null 2>&1; then
        for pid in $(pgrep -x fetchflow); do kill -9 "$pid" 2>/dev/null || true; done
    fi
    warn "Stopped the running FetchFlow instance."
fi

VERSION="$(grep -E '^VERSION=' "${INSTALLER_DIR}/version.env" | cut -d= -f2)"
[ -n "$VERSION" ] || die "Could not read VERSION from ${INSTALLER_DIR}/version.env"
log "Building version ${VERSION}"

# --- 1. Test gate -----------------------------------------------------------
log "Running automated test suite"
dotnet build app/XDM/XDM.Tests/XDM.Tests.csproj -c Release --nologo -v quiet >/dev/null
if dotnet "$TEST_RUNNER" | tee /tmp/fetchflow-tests.log | tail -4; then :; else
    die "Test runner exited non-zero — aborting before packaging."
fi
if grep -q '\[FAIL\]' /tmp/fetchflow-tests.log; then
    die "Test suite reported failures — aborting before packaging."
fi
rm -f /tmp/fetchflow-tests.log

# --- 2. Publish -------------------------------------------------------------
log "Publishing self-contained single-file binary"
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"
dotnet restore "$GTK_PROJECT" -r linux-x64 >/dev/null
dotnet publish "$GTK_PROJECT" -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true -o "$PUBLISH_DIR" --nologo -v quiet
[ -x "${PUBLISH_DIR}/fetchflow" ] || die "Publish did not produce ${PUBLISH_DIR}/fetchflow"

# --- 3. Stage runtime assets ------------------------------------------------
log "Staging runtime assets"

# Copy a path into the publish dir when it exists.
stage() {
    if [ -e "$1" ]; then
        cp -r "$1" "$2/"
    else
        warn "Skipping missing asset: $1"
    fi
}

stage app/XDM/chrome-extension         "$PUBLISH_DIR"
stage app/XDM/firefox-amo              "${PUBLISH_DIR}/firefox-extension"
stage app/XDM/XDM.Gtk.UI/svg-icons     "$PUBLISH_DIR"
stage app/XDM/XDM.Gtk.UI/glade         "$PUBLISH_DIR"
stage app/XDM/XDM.Gtk.UI/theme         "$PUBLISH_DIR"
stage app/XDM/XDM.Gtk.UI/images        "$PUBLISH_DIR"
stage app/XDM/Lang                     "$PUBLISH_DIR"

# The brand icon must exist under every name the desktop entry, packagers and themes look for.
[ -f "$BRAND_SVG" ] || die "Brand icon missing: ${BRAND_SVG}"
cp "$BRAND_SVG" "${PUBLISH_DIR}/com.mayanktaker.fetchflow.svg"
cp "$BRAND_SVG" "${PUBLISH_DIR}/fetchflow.svg"
cp "$BRAND_SVG" "${PUBLISH_DIR}/fetchflow-logo.svg"

# All raster sizes the theme looks for (16 .. 512).
for png in app/XDM/XDM.Gtk.UI/fetchflow-logo*.png; do
    if [ -e "$png" ]; then cp "$png" "$PUBLISH_DIR/"; fi
done
if [ ! -e "${PUBLISH_DIR}/fetchflow-logo.png" ] && [ -f app/XDM/XDM.Gtk.UI/fetchflow-logo.png ]; then
    cp app/XDM/XDM.Gtk.UI/fetchflow-logo.png "$PUBLISH_DIR/"
fi

# The tray glyph is what stops GNOME showing a generic "..." placeholder.
[ -f "${PUBLISH_DIR}/${TRAY_SVG}" ] \
    || die "${TRAY_SVG} missing from publish output (tray icon would break)"

# --- 4. Package -------------------------------------------------------------
log "Building Arch package"
cd "$INSTALLER_DIR"
mkdir -p binary-source
rm -rf binary-source/*
cp -r "${PUBLISH_DIR}/." binary-source/
bash make-arch-pkg >/dev/null

PKG="$(find . -maxdepth 1 -name 'fetchflow-*.pkg.tar.zst' -printf '%T@ %f\n' 2>/dev/null | sort -rn | head -1 | cut -d' ' -f2- || true)"
[ -n "$PKG" ] || die "makepkg did not produce a .pkg.tar.zst package"
PKG_PATH="${INSTALLER_DIR}/${PKG}"
log "Built ${PKG} ($(du -h "$PKG_PATH" | cut -f1))"

# Sanity-check the payload before handing it to pacman.
bsdtar -tf "$PKG_PATH" | grep -q "opt/fetchflow/fetchflow$" \
    || die "Package is missing opt/fetchflow/fetchflow"
if ! bsdtar -tf "$PKG_PATH" | grep -q "icons/hicolor/scalable/apps/fetchflow-tray.svg"; then
    warn "Package has no themed tray icon (app will self-install one on first run)"
fi

if [ "$DO_INSTALL" -eq 0 ]; then
    log "Built only (--no-install). Package is at: ${PKG_PATH}"
    exit 0
fi

# --- 5. Install -------------------------------------------------------------
command -v pacman >/dev/null || die "pacman not found — this script is for Arch-family systems only."
if [ "$ASSUME_YES" -eq 0 ]; then
    log "About to install ${PKG} with pacman (this needs your sudo password)"
    read -r -p "Continue? [y/N] " reply
    case "$reply" in
        y|Y|yes|YES) ;;
        *) die "Aborted by user." ;;
    esac
fi

sudo pacman -U "$PKG_PATH"

# --- 6. Post-install repair + verify ---------------------------------------
log "Repairing launch-at-login entry"
AUTOSTART_DIR="${XDG_CONFIG_HOME:-${HOME}/.config}/autostart"
AUTOSTART_FILE="${AUTOSTART_DIR}/com.mayanktaker.fetchflow.desktop"
mkdir -p "$AUTOSTART_DIR"

# Always repoint at the real install: a stale path silently breaks login autostart.
cat > "$AUTOSTART_FILE" <<'EOF'
[Desktop Entry]
Version=1.0
Type=Application
Terminal=false
TryExec=/opt/fetchflow/fetchflow
Exec=env GTK_USE_PORTAL=1 "/opt/fetchflow/fetchflow" --background
Name=FetchFlow Download Manager
Comment=FetchFlow Download Manager (Wayland Edition)
Categories=Network;
Icon=/opt/fetchflow/fetchflow-logo.svg
X-GNOME-Autostart-enabled=true
StartupNotify=false
X-FetchFlow-Autostart=1
EOF
chmod +x "$AUTOSTART_FILE"

if command -v desktop-file-validate >/dev/null; then
    if desktop-file-validate "$AUTOSTART_FILE"; then
        log "Autostart entry is valid"
    else
        warn "Autostart entry failed desktop-file-validate"
    fi
fi

# Drop any user-local tray icon left by an older build so the new artwork is picked up.
USER_TRAY="${XDG_DATA_HOME:-${HOME}/.local/share}/icons/hicolor/scalable/apps/fetchflow-tray.svg"
rm -f "$USER_TRAY" || true

log "Verifying"
if command -v pacman >/dev/null; then
    printf '  installed version : %s\n' "$(pacman -Q fetchflow 2>/dev/null || echo 'not found')"
fi
if [ -f /opt/fetchflow/fetchflow ]; then
    printf '  autostart target  : present\n'
else
    printf '  autostart target  : MISSING\n'
fi
printf '  tray icon (system): %s\n' \
    "$( [ -f /usr/share/icons/hicolor/scalable/apps/fetchflow-tray.svg ] && echo present || echo absent )"

log "Done. Launch FetchFlow from your menu, or run: /opt/fetchflow/fetchflow"
