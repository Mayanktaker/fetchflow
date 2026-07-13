#!/bin/bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
set -e

export DOTNET_ROOT=/home/mayanktakeroffice/.dotnet
export PATH=$DOTNET_ROOT:$PATH

# Create and clean output directory
OUT_DIR="$(pwd)/xdm-release"
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

echo "Packaging Chrome Extension..."
cd app/XDM/chrome-extension
zip -r "$OUT_DIR/xdm-chrome-extension-9.1.0.zip" .
cd ../../..

echo "Packaging Firefox Extension..."
cd app/XDM/firefox-amo
if [ -n "$MOZ_API_KEY" ] && [ -n "$MOZ_API_SECRET" ]; then
    echo "Signing Firefox extension with web-ext..."
    npx -y web-ext sign --api-key="$MOZ_API_KEY" --api-secret="$MOZ_API_SECRET" --channel=unlisted --artifacts-dir=web-ext-artifacts
    cp web-ext-artifacts/*.xpi "$OUT_DIR/xdm-firefox-extension-9.1.0.xpi"
else
    echo "No Mozilla API keys provided, creating unsigned XPI..."
    zip -r "$OUT_DIR/xdm-firefox-extension-9.1.0.xpi" .
fi
cd ../../..

echo "Publishing XDM .NET Application..."
rm -rf build_output
mkdir -p build_output
dotnet publish app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o build_output/xdm-app

echo "Packaging Portable Tarball..."
cd build_output/xdm-app
tar -czvf "$OUT_DIR/xdm-linux-x64-9.1.0.tar.gz" *
cd ../..

echo "Creating RPM Package..."
cd app/XDM/XDM.Linux.Installer
rm -rf rpmbuild xdm-9.1.0.tar.gz xdm-9.1.0
# Ensure binary-source is fresh
rm -rf binary-source/*
cp -r ../../../build_output/xdm-app/* binary-source/

bash make-rpm-pkg
bash make-deb-pkg || echo "make-deb-pkg failed, skipping deb package"

# Copy the packages back to the root xdm-release folder
cp rpmbuild/RPMS/x86_64/xdm-9.1.0-1.fc44.x86_64.rpm "$OUT_DIR/"
cp *.deb "$OUT_DIR/" 2>/dev/null || echo "No .deb packages found to copy"

echo "========================================="
echo "Build complete! Artifacts are in $OUT_DIR"
ls -la "$OUT_DIR"
echo "========================================="
