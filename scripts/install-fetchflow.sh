#!/usr/bin/env bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# Universal FetchFlow installer for Arch-family (Arch/CachyOS/Manjaro/EndeavourOS/Garuda),
# Fedora/RHEL, Debian/Ubuntu, openSUSE, and generic Linux (tarball fallback).
# Usage: curl -fsSL https://github.com/Mayanktaker/fetchflow/releases/latest/download/install-fetchflow.sh | bash
#        bash install-fetchflow.sh [--version X.Y.Z.W] [--yes] [--uninstall] [--check] [--help]
set -euo pipefail

# Config tokens (no magic values outside this block)
REPO="Mayanktaker/fetchflow"
APP_ID="com.mayanktaker.fetchflow"
INSTALL_DIR="/opt/fetchflow"
BIN_LINK="/usr/bin/fetchflow"
VERSION_DEFAULT="9.1.15.15"
VERSION="${FETCHFLOW_VERSION:-$VERSION_DEFAULT}"

# CLI flags
ASSUME_YES=0
DO_UNINSTALL=0
DO_CHECK=0

# Print usage
usage() {
    echo "FetchFlow universal installer — Arch/CachyOS/Manjaro, Fedora, Debian/Ubuntu, openSUSE, generic tarball"
    echo "Usage: $0 [--version X.Y.Z.W] [--yes] [--uninstall] [--check] [--help]"
}

# Parse args
while [ $# -gt 0 ]; do
    case "$1" in
        --version=*) VERSION="${1#*=}"; shift ;;
        --version) VERSION="${2:-$VERSION_DEFAULT}"; shift 2 || shift ;;
        -y|--yes|--noconfirm) ASSUME_YES=1; shift ;;
        --uninstall|--remove) DO_UNINSTALL=1; shift ;;
        --check|--verify) DO_CHECK=1; shift ;;
        -h|--help) usage; exit 0 ;;
        [0-9]*.[0-9]*) VERSION="$1"; shift ;;
        *) echo "Unknown option: $1" >&2; usage; exit 2 ;;
    esac
done

# Logging helpers
log() { echo "[fetchflow] $*"; }
die() { echo "[fetchflow] ERROR: $*" >&2; exit 1; }

# Detect distro family via /etc/os-release
detect_family() {
    local id="" like="";
    if [ -r /etc/os-release ]; then
        # shellcheck disable=SC1091
        . /etc/os-release
        id="$(echo "${ID:-}" | tr '[:upper:]' '[:lower:]')";
        like="$(echo "${ID_LIKE:-}" | tr '[:upper:]' '[:lower:]')";
    fi
    case " $like $id " in
        *" arch "*) echo "arch" ;;
        *" fedora "*|*" rhel "*|*" centos "*) echo "fedora" ;;
        *" debian "*|*" ubuntu "*) echo "debian" ;;
        *" suse "*|*" opensuse "*) echo "suse" ;;
        *) case "$id" in
            arch|cachyos|manjaro|endeavouros|garuda|artix|arco) echo "arch" ;;
            fedora|rhel|centos|rocky|alma*|nobara) echo "fedora" ;;
            debian|ubuntu|linuxmint|mint|pop*|zorin|kali|elementary) echo "debian" ;;
            opensuse*|suse|sle*) echo "suse" ;;
            *) echo "generic" ;;
        esac ;;
    esac
}

# Ensure root for system install
need_root() {
    if [ "$(id -u)" -ne 0 ]; then
        if command -v sudo >/dev/null 2>&1; then
            log "Re-executing with sudo…";
            extra="";
            [ $ASSUME_YES = 1 ] && extra="$extra --yes";
            [ $DO_UNINSTALL = 1 ] && extra="$extra --uninstall";
            [ $DO_CHECK = 1 ] && extra="$extra --check";
            script="$0";
            # Piped install (curl|bash): $0 is not a file, so stage a stable copy
            if [ ! -f "$script" ]; then
                script="$(mktemp /tmp/fetchflow-install.XXXXXX.sh)";
                fetch "https://github.com/$REPO/releases/latest/download/install-fetchflow.sh" "$script" || die "Could not stage installer for sudo re-exec.";
            fi
            # shellcheck disable=SC2086
            exec sudo FETCHFLOW_VERSION="$VERSION" bash "$script" $extra --version="$VERSION";
        else
            die "Run as root (or install sudo).";
        fi
    fi
}

# Download helper (curl/wget)
fetch() {
    local url="$1" out="$2";
    if command -v curl >/dev/null 2>&1; then curl -fsSL -o "$out" "$url";
    elif command -v wget >/dev/null 2>&1; then wget -qO "$out" "$url";
    else die "Need curl or wget to download $url"; fi
}

# Resolve exact RPM asset name via GitHub API (dist tag varies: fc42/fc44…)
resolve_rpm_url() {
    local api="https://api.github.com/repos/$REPO/releases/tags/v$VERSION" url="";
    if command -v curl >/dev/null 2>&1; then
        url="$(curl -fsSL "$api" 2>/dev/null | grep -oE 'https://[^"]*\.rpm' | head -n1 || true)";
    elif command -v wget >/dev/null 2>&1; then
        url="$(wget -qO- "$api" 2>/dev/null | grep -oE 'https://[^"]*\.rpm' | head -n1 || true)";
    fi
    echo "$url"
}

# Uninstall all layouts
do_uninstall() {
    need_root;
    log "Removing FetchFlow…";
    if command -v pacman >/dev/null 2>&1 && pacman -Q fetchflow >/dev/null 2>&1; then pacman -R --noconfirm fetchflow || :; fi
    if command -v dnf >/dev/null 2>&1 && rpm -q fetchflow >/dev/null 2>&1; then dnf remove -y fetchflow || :; fi
    if command -v zypper >/dev/null 2>&1 && rpm -q fetchflow >/dev/null 2>&1; then zypper -n remove fetchflow || :; fi
    if command -v apt >/dev/null 2>&1 && dpkg -s fetchflow >/dev/null 2>&1; then apt remove -y fetchflow || :; fi
    rm -rf "$INSTALL_DIR" "$BIN_LINK" "/usr/bin/xdman" "/usr/share/applications/$APP_ID.desktop" || :;
    log "Uninstalled. User data in ~/.fetchflow-app-data left untouched.";
    exit 0
}

# Verify install
do_check() {
    if [ -x "$INSTALL_DIR/fetchflow" ] || command -v fetchflow >/dev/null 2>&1; then
        log "OK: FetchFlow installed (${INSTALL_DIR}/fetchflow).";
        exit 0;
    else
        die "FetchFlow not found. Run without --check to install.";
    fi
}

# Tarball fallback install (all distros)
install_tarball() {
    local ver="$1" tmp="";
    tmp="$(mktemp -d)";
    # shellcheck disable=SC2064
    trap "rm -rf '$tmp'" EXIT;
    log "Downloading portable tarball v$ver…";
    fetch "https://github.com/$REPO/releases/download/v$ver/fetchflow-linux-x64-$ver.tar.gz" "$tmp/pkg.tar.gz";
    log "Extracting to $INSTALL_DIR…";
    mkdir -p "$INSTALL_DIR";
    tar -xzf "$tmp/pkg.tar.gz" -C "$INSTALL_DIR";
    chmod +x "$INSTALL_DIR/fetchflow";
    write_integration;
    rm -rf "$tmp"; trap - EXIT;
}

# Shared desktop integration for tarball/generic path
write_integration() {
    cat > "/usr/share/applications/$APP_ID.desktop" <<EOF
[Desktop Entry]
Version=1.0
Encoding=UTF-8
Exec=env GTK_USE_PORTAL=1 $INSTALL_DIR/fetchflow %U
Type=Application
Terminal=false
Name=FetchFlow Download Manager
Comment=Fast, modern download accelerator and video downloader
Categories=Network;FileTransfer;
Icon=$APP_ID
MimeType=application/fetchflow;x-scheme-handler/fetchflow;x-scheme-handler/fetchflow+app;x-scheme-handler/xdm-app;x-scheme-handler/xdm+app;
StartupNotify=true
StartupWMClass=$APP_ID
EOF
    cat > "$BIN_LINK" <<EOF
#!/bin/bash
export GTK_USE_PORTAL=1
exec $INSTALL_DIR/fetchflow "\$@"
EOF
    chmod 755 "$BIN_LINK";
    ln -sf "$BIN_LINK" /usr/bin/xdman;
    for sz in 16 22 24 32 48 64 128 256 512; do
        if [ -f "$INSTALL_DIR/fetchflow-logo-$sz.png" ]; then
            mkdir -p "/usr/share/icons/hicolor/${sz}x${sz}/apps";
            cp -f "$INSTALL_DIR/fetchflow-logo-$sz.png" "/usr/share/icons/hicolor/${sz}x${sz}/apps/fetchflow.png";
            cp -f "$INSTALL_DIR/fetchflow-logo-$sz.png" "/usr/share/icons/hicolor/${sz}x${sz}/apps/$APP_ID.png";
        fi
    done
    if [ -f "$INSTALL_DIR/fetchflow-logo.svg" ]; then
        mkdir -p /usr/share/icons/hicolor/scalable/apps;
        cp -f "$INSTALL_DIR/fetchflow-logo.svg" "/usr/share/icons/hicolor/scalable/apps/fetchflow.svg";
        cp -f "$INSTALL_DIR/fetchflow-logo.svg" "/usr/share/icons/hicolor/scalable/apps/$APP_ID.svg";
    fi
    update-desktop-database -q /usr/share/applications 2>/dev/null || :;
    gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor 2>/dev/null || :;
}

# Per-family flows
install_arch() {
    local ver="$1" tmp="" pkg="";
    tmp="$(mktemp -d)";
    # shellcheck disable=SC2064
    trap "rm -rf '$tmp'" EXIT;
    log "Installing deps (gtk3 ffmpeg xdg-desktop-portal libnotify)…";
    pacman -Sy --needed --noconfirm gtk3 ffmpeg xdg-desktop-portal libnotify desktop-file-utils yt-dlp || pacman -Sy --needed --noconfirm gtk3 ffmpeg xdg-desktop-portal libnotify desktop-file-utils;
    pkg="$tmp/fetchflow-$ver-1-x86_64.pkg.tar.zst";
    log "Downloading Arch package v$ver…";
    if ! fetch "https://github.com/$REPO/releases/download/v$ver/fetchflow-$ver-1-x86_64.pkg.tar.zst" "$pkg"; then
        log "Prebuilt Arch package not found — falling back to tarball.";
        rm -rf "$tmp"; trap - EXIT;
        install_tarball "$ver"; return;
    fi
    pacman -U --noconfirm "$pkg";
    log "Done. AUR users can alternatively run: yay -S fetchflow-bin";
    rm -rf "$tmp"; trap - EXIT;
}

install_fedora() {
    local ver="$1" tmp="" rpm="";
    tmp="$(mktemp -d)";
    # shellcheck disable=SC2064
    trap "rm -rf '$tmp'" EXIT;
    rpm="$(resolve_rpm_url)";
    [ -n "$rpm" ] || die "Could not resolve .rpm asset for v$ver (check version exists).";
    log "Downloading $rpm…";
    fetch "$rpm" "$tmp/pkg.rpm";
    if command -v dnf >/dev/null 2>&1; then dnf install -y "$tmp/pkg.rpm";
    else yum install -y "$tmp/pkg.rpm"; fi
    rm -rf "$tmp"; trap - EXIT;
}

install_debian() {
    local ver="$1" tmp="";
    tmp="$(mktemp -d)";
    # shellcheck disable=SC2064
    trap "rm -rf '$tmp'" EXIT;
    log "Downloading DEB v$ver…";
    fetch "https://github.com/$REPO/releases/download/v$ver/fetchflow_${ver}_amd64.deb" "$tmp/pkg.deb";
    apt-get update;
    if [ $ASSUME_YES = 1 ]; then apt-get install -y "$tmp/pkg.deb"; else apt install -y "$tmp/pkg.deb"; fi
    rm -rf "$tmp"; trap - EXIT;
}

install_suse() {
    local ver="$1" tmp="" rpm="";
    tmp="$(mktemp -d)";
    # shellcheck disable=SC2064
    trap "rm -rf '$tmp'" EXIT;
    rpm="$(resolve_rpm_url)";
    [ -n "$rpm" ] || die "Could not resolve .rpm asset for v$ver.";
    fetch "$rpm" "$tmp/pkg.rpm";
    zypper --non-interactive install --allow-unsigned-rpm "$tmp/pkg.rpm";
    rm -rf "$tmp"; trap - EXIT;
}

# Main
[ $DO_UNINSTALL = 1 ] && do_uninstall;
[ $DO_CHECK = 1 ] && do_check;
need_root;
FAMILY="$(detect_family)";
log "Detected family: $FAMILY (version $VERSION).";
case "$FAMILY" in
    arch) install_arch "$VERSION" ;;
    fedora) install_fedora "$VERSION" ;;
    debian) install_debian "$VERSION" ;;
    suse) install_suse "$VERSION" ;;
    *) log "Generic Linux — tarball install.";
       if command -v pacman >/dev/null 2>&1; then pacman -Sy --needed --noconfirm gtk3 ffmpeg xdg-desktop-portal libnotify 2>/dev/null || :; fi
       install_tarball "$VERSION" ;;
esac
do_check;
