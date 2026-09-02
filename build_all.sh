#!/bin/bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
set -e

if [ -x "/mnt/Development/_DevSofts/dotnet8/dotnet" ]; then
    export DOTNET_ROOT=/mnt/Development/_DevSofts/dotnet8
elif [ -x "/home/mayanktakeroffice/.dotnet8/dotnet" ]; then
    export DOTNET_ROOT=/home/mayanktakeroffice/.dotnet8
elif [ -d "$HOME/.dotnet8" ]; then
    export DOTNET_ROOT="$HOME/.dotnet8"
fi
export PATH=$DOTNET_ROOT:$PATH

# User-local rpmbuild (extracted rpm-build package; see AGENTS.md)
export PATH="$HOME/.local/rpm-build-root/usr/bin:$PATH"

# Fail fast when required tools are missing instead of producing an incomplete release
for tool in dotnet zip tar rpmbuild; do
    command -v "$tool" >/dev/null 2>&1 || { echo "ERROR: required tool '$tool' not found in PATH — install it before generating a release." >&2; exit 1; }
done

# Optional packagers: warn up front so an incomplete release never surprises us
for tool in dpkg-deb makepkg; do
    command -v "$tool" >/dev/null 2>&1 || echo "WARNING: '$tool' not found — DEB/Arch packages will be skipped for this release."
done

# Single source of truth for the version
source app/XDM/XDM.Linux.Installer/version.env

# Pre-release verification gate: compile and run full automated test suite
echo "=== Running pre-release verification tests ==="
dotnet build app/XDM/XDM.Tests/XDM.Tests.csproj -c Release
"$DOTNET_ROOT/dotnet" app/XDM/XDM.Tests/bin/Release/net8.0/XDM.Tests.dll || {
    echo "ERROR: Pre-release verification test suite failed. Aborting release build." >&2
    exit 1
}
echo "=== Pre-release tests passed successfully ==="

# Create and clean output directory
OUT_DIR="$(pwd)/xdm-release"
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

echo "Packaging Chrome Extension..."
cd app/XDM/chrome-extension
zip -r "$OUT_DIR/fetchflow-chrome-extension-${VERSION}.zip" . -x ".*"
cd ../../..

echo "Packaging Firefox Extension..."
cd app/XDM/firefox-amo
if [ -n "$MOZ_API_KEY" ] && [ -n "$MOZ_API_SECRET" ]; then
    echo "Signing Firefox extension with web-ext..."
    npx -y web-ext sign --api-key="$MOZ_API_KEY" --api-secret="$MOZ_API_SECRET" --channel=unlisted --artifacts-dir=web-ext-artifacts
    cp web-ext-artifacts/*.xpi "$OUT_DIR/fetchflow-firefox-extension-${VERSION}.xpi"
else
    echo "No Mozilla API keys provided, creating unsigned XPI..."
    zip -r "$OUT_DIR/fetchflow-firefox-extension-${VERSION}.xpi" . -x ".*"
fi
cd ../../..

echo "Publishing FetchFlow .NET Application..."
rm -rf build_output
mkdir -p build_output/xdm-app
dotnet publish app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o build_output/xdm-app

# Copy all runtime assets into publish directory
cp -r app/XDM/chrome-extension build_output/xdm-app/
cp -r app/XDM/firefox-amo build_output/xdm-app/firefox-extension
cp -r app/XDM/XDM.Gtk.UI/svg-icons build_output/xdm-app/
cp -r app/XDM/XDM.Gtk.UI/glade build_output/xdm-app/
cp -r app/XDM/XDM.Gtk.UI/theme build_output/xdm-app/
cp -r app/XDM/XDM.Gtk.UI/images build_output/xdm-app/
cp -r app/XDM/Lang build_output/xdm-app/
cp app/XDM/fetchflow-logo.svg build_output/xdm-app/ 2>/dev/null || :
cp app/XDM/XDM.Gtk.UI/fetchflow-logo-512.png build_output/xdm-app/ 2>/dev/null || :
cp app/XDM/XDM.Gtk.UI/fetchflow-logo.png build_output/xdm-app/ 2>/dev/null || :

echo "Packaging Portable Tarball..."
cd build_output/xdm-app
tar -czvf "$OUT_DIR/fetchflow-linux-x64-${VERSION}.tar.gz" *
cd ../..

echo "Creating RPM Package..."
cd app/XDM/XDM.Linux.Installer
rm -rf rpmbuild fetchflow-${VERSION}.tar.gz fetchflow-${VERSION}
# Ensure binary-source is fresh
mkdir -p binary-source
rm -rf binary-source/*
cp -r ../../../build_output/xdm-app/* binary-source/

# RPM is mandatory for every release — a failed RPM build must abort the release
bash make-rpm-pkg || { echo "ERROR: RPM package build failed — every release MUST include the .rpm artifact." >&2; exit 1; }
bash make-deb-pkg || echo "WARNING: DEB package skipped (dpkg-deb missing or failed) — this release is incomplete."
bash make-arch-pkg || echo "WARNING: Arch package skipped (makepkg missing or failed) — this release is incomplete."

# Copy the packages back to the root xdm-release folder
cp rpmbuild/RPMS/x86_64/fetchflow-${VERSION}*.rpm "$OUT_DIR/" 2>/dev/null || echo "No .rpm packages found to copy"
cp fetchflow_${VERSION}*.deb "$OUT_DIR/" 2>/dev/null || echo "No .deb packages found to copy"
cp fetchflow-${VERSION}*.pkg.tar.* "$OUT_DIR/" 2>/dev/null || echo "No .pkg.tar.* packages found to copy"
cd ../../..

echo "Generating SHA256 Checksums..."
cd "$OUT_DIR"
rm -f SHA256SUMS.txt
sha256sum * > SHA256SUMS.txt 2>/dev/null || true
cd ..

echo "========================================="
echo "Build complete! Artifacts are in $OUT_DIR"
ls -la "$OUT_DIR"
echo "========================================="
