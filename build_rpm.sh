#!/bin/bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
set -e

export DOTNET_ROOT=/home/mayanktakeroffice/.dotnet8
export PATH=$DOTNET_ROOT:$PATH
export PATH="$HOME/.local/rpm-build-root/usr/bin:$PATH"

source app/XDM/XDM.Linux.Installer/version.env

# Create output directory
OUT_DIR="$(pwd)/xdm-release"
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

echo "Packaging Chrome Extension..."
cd app/XDM/chrome-extension
zip -r "$OUT_DIR/fetchflow-chrome-extension-${VERSION}.zip" . -x ".*"
cd ../../..

echo "Packaging Firefox Extension..."
cd app/XDM/firefox-amo
zip -r "$OUT_DIR/fetchflow-firefox-extension-${VERSION}.xpi" . -x ".*"
cd ../../..

echo "Publishing FetchFlow .NET Application..."
rm -rf build_output
mkdir -p build_output
dotnet publish app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o build_output/xdm-app

echo "Creating RPM Package..."
cd app/XDM/XDM.Linux.Installer
rm -rf rpmbuild fetchflow-${VERSION}.tar.gz fetchflow-${VERSION}
rm -rf binary-source/*
cp -r ../../../build_output/xdm-app/* binary-source/

bash make-rpm-pkg
cp rpmbuild/RPMS/x86_64/*.rpm "$OUT_DIR/"

echo "Done! Artifacts are in $OUT_DIR"
