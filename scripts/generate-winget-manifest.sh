#!/bin/bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
set -e

# Prints script usage instructions
usage() {
    cat <<EOF
Usage: $0 [OPTIONS]

Generates Windows Package Manager (Winget) v1.6.0 manifests for FetchFlow.

Options:
  -v, --version <ver>         Override package version (default: from version.env)
  -o, --output-dir <dir>      Output root directory (default: winget-manifests)
  --installer-sha <sha256>    Explicit SHA-256 for Windows setup .exe
  --portable-sha <sha256>     Explicit SHA-256 for Windows portable .zip
  -h, --help                  Show this help message

EOF
    exit 0
}

# Resolve root directory of the repository
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

# Resolve default version from single source of truth
DEFAULT_VERSION="9.1.15.12"
if [ -f "app/XDM/XDM.Linux.Installer/version.env" ]; then
    # shellcheck disable=SC1091
    source "app/XDM/XDM.Linux.Installer/version.env"
    APP_VERSION="$VERSION"
else
    APP_VERSION="$DEFAULT_VERSION"
fi

OUTPUT_DIR="winget-manifests"
EXPLICIT_INSTALLER_SHA=""
EXPLICIT_PORTABLE_SHA=""

# Parse command line options
while [[ $# -gt 0 ]]; do
    case "$1" in
        -v|--version)
            APP_VERSION="$2"
            shift 2
            ;;
        -o|--output-dir)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --installer-sha)
            EXPLICIT_INSTALLER_SHA="$2"
            shift 2
            ;;
        --portable-sha)
            EXPLICIT_PORTABLE_SHA="$2"
            shift 2
            ;;
        -h|--help)
            usage
            ;;
        *)
            echo "Unknown option: $1" >&2
            usage
            ;;
    esac
done

echo "=== Generating Winget Manifests for FetchFlow v${APP_VERSION} ==="

RELEASE_DIR="fetchflow-release"
INSTALLER_FILE="${RELEASE_DIR}/fetchflow-windows-x64-setup-${APP_VERSION}.exe"
PORTABLE_FILE="${RELEASE_DIR}/fetchflow-windows-x64-portable-${APP_VERSION}.zip"
SHA_FILE="${RELEASE_DIR}/SHA256SUMS.txt"

# Resolves SHA-256 hash for a given artifact file
resolve_sha256() {
    local target_file="$1"
    local explicit_sha="$2"
    local basename
    basename="$(basename "$target_file")"

    if [ -n "$explicit_sha" ]; then
        echo "$explicit_sha"
        return 0
    fi

    if [ -f "$target_file" ]; then
        sha256sum "$target_file" | awk '{print $1}'
        return 0
    fi

    if [ -f "$SHA_FILE" ]; then
        local found_sha
        found_sha=$(grep "  $basename" "$SHA_FILE" | head -n 1 | awk '{print $1}')
        if [ -n "$found_sha" ]; then
            echo "$found_sha"
            return 0
        fi
    fi

    # Fallback placeholder when installer is compiled in CI
    echo "0000000000000000000000000000000000000000000000000000000000000000"
}

INSTALLER_SHA=$(resolve_sha256 "$INSTALLER_FILE" "$EXPLICIT_INSTALLER_SHA")
PORTABLE_SHA=$(resolve_sha256 "$PORTABLE_FILE" "$EXPLICIT_PORTABLE_SHA")

# Package identifier metadata
PACKAGE_ID="Mayanktaker.FetchFlow"
PACKAGE_NAME="FetchFlow Download Manager"
PUBLISHER="Mayanktaker Computers & Web Development"
PUBLISHER_URL="https://mayanktaker.com"
PACKAGE_URL="https://github.com/Mayanktaker/fetchflow"
LICENSE_URL="https://github.com/Mayanktaker/fetchflow/blob/master/LICENSE"
PRIVACY_URL="https://mayanktaker.github.io/fetchflow/privacy.html"
RELEASE_NOTES_URL="https://github.com/Mayanktaker/fetchflow/releases/tag/v${APP_VERSION}"
INSTALLER_URL="https://github.com/Mayanktaker/fetchflow/releases/download/v${APP_VERSION}/fetchflow-windows-x64-setup-${APP_VERSION}.exe"
PORTABLE_URL="https://github.com/Mayanktaker/fetchflow/releases/download/v${APP_VERSION}/fetchflow-windows-x64-portable-${APP_VERSION}.zip"

# Create winget-pkgs standardized directory structure: manifests/m/Mayanktaker/FetchFlow/<version>/
TARGET_DIR="${OUTPUT_DIR}/manifests/m/Mayanktaker/FetchFlow/${APP_VERSION}"
mkdir -p "$TARGET_DIR"

echo "Writing manifests to: $TARGET_DIR"

# 1. Version Manifest
VERSION_MANIFEST="${TARGET_DIR}/${PACKAGE_ID}.yaml"
cat <<EOF > "$VERSION_MANIFEST"
# yaml-language-server: \$schema=https://aka.ms/winget-manifest.version.1.6.0.schema.json

PackageIdentifier: ${PACKAGE_ID}
PackageVersion: ${APP_VERSION}
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.6.0
EOF

# 2. Installer Manifest (Inno Setup & Portable ZIP)
INSTALLER_MANIFEST="${TARGET_DIR}/${PACKAGE_ID}.installer.yaml"
cat <<EOF > "$INSTALLER_MANIFEST"
# yaml-language-server: \$schema=https://aka.ms/winget-manifest.installer.1.6.0.schema.json

PackageIdentifier: ${PACKAGE_ID}
PackageVersion: ${APP_VERSION}
InstallerLocale: en-US
MinimumOSVersion: 10.0.0.0
InstallerType: inno
Scope: user
InstallModes:
  - interactive
  - silent
  - silentWithProgress
InstallerSwitches:
  Silent: /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
  SilentWithProgress: /SILENT /SUPPRESSMSGBOXES /NORESTART
UpgradeBehavior: install
Commands:
  - fetchflow
Protocols:
  - http
  - https
FileExtensions:
  - crx
  - xpi
Installers:
  - Architecture: x64
    InstallerType: inno
    InstallerUrl: ${INSTALLER_URL}
    InstallerSha256: ${INSTALLER_SHA}
  - Architecture: x64
    InstallerType: portable
    InstallerUrl: ${PORTABLE_URL}
    InstallerSha256: ${PORTABLE_SHA}
    Commands:
      - fetchflow
ManifestType: installer
ManifestVersion: 1.6.0
EOF

# 3. Default Locale Manifest
LOCALE_MANIFEST="${TARGET_DIR}/${PACKAGE_ID}.locale.en-US.yaml"
cat <<EOF > "$LOCALE_MANIFEST"
# yaml-language-server: \$schema=https://aka.ms/winget-manifest.defaultLocale.1.6.0.schema.json

PackageIdentifier: ${PACKAGE_ID}
PackageVersion: ${APP_VERSION}
PackageLocale: en-US
Publisher: ${PUBLISHER}
PublisherUrl: ${PUBLISHER_URL}
PublisherSupportUrl: ${PUBLISHER_URL}
PrivacyUrl: ${PRIVACY_URL}
Author: Mayanktaker
PackageName: ${PACKAGE_NAME}
PackageUrl: ${PACKAGE_URL}
License: GPL-3.0-or-later
LicenseUrl: ${LICENSE_URL}
Copyright: © Mayanktaker Computers & Web Development
ShortDescription: High-performance, multi-threaded download manager and media grabber with browser integration.
Description: |-
  FetchFlow is a fast, lightweight, and modern download manager built for speed, stability, and control.
  Features include multi-threaded segment downloading, browser integration via Manifest V3 extensions,
  video and audio stream capture, clipboard monitoring, customizable color schemes, and zero telemetry.
Moniker: fetchflow
Tags:
  - download-manager
  - downloader
  - media-grabber
  - multithreaded
  - video-downloader
  - youtube-dl
  - yt-dlp
ReleaseNotesUrl: ${RELEASE_NOTES_URL}
ManifestType: defaultLocale
ManifestVersion: 1.6.0
EOF

# 4. Singleton Manifest (all-in-one manifest alternative)
SINGLETON_MANIFEST="${TARGET_DIR}/${PACKAGE_ID}.singleton.yaml"
cat <<EOF > "$SINGLETON_MANIFEST"
# yaml-language-server: \$schema=https://aka.ms/winget-manifest.singleton.1.6.0.schema.json

PackageIdentifier: ${PACKAGE_ID}
PackageVersion: ${APP_VERSION}
PackageLocale: en-US
Publisher: ${PUBLISHER}
PublisherUrl: ${PUBLISHER_URL}
PublisherSupportUrl: ${PUBLISHER_URL}
PrivacyUrl: ${PRIVACY_URL}
Author: Mayanktaker
PackageName: ${PACKAGE_NAME}
PackageUrl: ${PACKAGE_URL}
License: GPL-3.0-or-later
LicenseUrl: ${LICENSE_URL}
Copyright: © Mayanktaker Computers & Web Development
ShortDescription: High-performance, multi-threaded download manager and media grabber with browser integration.
Description: |-
  FetchFlow is a fast, lightweight, and modern download manager built for speed, stability, and control.
  Features include multi-threaded segment downloading, browser integration via Manifest V3 extensions,
  video and audio stream capture, clipboard monitoring, customizable color schemes, and zero telemetry.
Moniker: fetchflow
Tags:
  - download-manager
  - downloader
  - media-grabber
  - multithreaded
  - video-downloader
  - youtube-dl
  - yt-dlp
ReleaseNotesUrl: ${RELEASE_NOTES_URL}
MinimumOSVersion: 10.0.0.0
InstallerType: inno
Scope: user
InstallModes:
  - interactive
  - silent
  - silentWithProgress
InstallerSwitches:
  Silent: /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
  SilentWithProgress: /SILENT /SUPPRESSMSGBOXES /NORESTART
UpgradeBehavior: install
Commands:
  - fetchflow
Protocols:
  - http
  - https
FileExtensions:
  - crx
  - xpi
Installers:
  - Architecture: x64
    InstallerType: inno
    InstallerUrl: ${INSTALLER_URL}
    InstallerSha256: ${INSTALLER_SHA}
  - Architecture: x64
    InstallerType: portable
    InstallerUrl: ${PORTABLE_URL}
    InstallerSha256: ${PORTABLE_SHA}
    Commands:
      - fetchflow
ManifestType: singleton
ManifestVersion: 1.6.0
EOF

# Validate generated YAML syntax using Python if available
if command -v python3 >/dev/null 2>&1; then
    echo "=== Validating YAML syntax ==="
    for f in "$VERSION_MANIFEST" "$INSTALLER_MANIFEST" "$LOCALE_MANIFEST" "$SINGLETON_MANIFEST"; do
        python3 -c "import yaml; yaml.safe_load(open('$f'))" || {
            echo "ERROR: YAML validation failed for $f" >&2
            exit 1
        }
        echo "  [OK] $(basename "$f")"
    done
fi

echo "=========================================================="
echo "Winget manifests generated successfully!"
echo "Version:           v${APP_VERSION}"
echo "Installer SHA-256: ${INSTALLER_SHA}"
echo "Portable SHA-256:  ${PORTABLE_SHA}"
echo "Location:          ${TARGET_DIR}"
echo "=========================================================="
