#!/bin/bash
set -e

export DOTNET_ROOT=/home/mayanktakeroffice/.dotnet
export PATH=$DOTNET_ROOT:$PATH

# Create output directory
OUT_DIR="$(pwd)/xdm-release"
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

echo "Packaging Chrome Extension..."
cd app/XDM/chrome-extension
zip -r "$OUT_DIR/xdm-chrome-extension.zip" *
cd ../../..

echo "Packaging Firefox Extension..."
cd app/XDM/firefox-amo
zip -r "$OUT_DIR/xdm-firefox-extension.xpi" *
cd ../../..

echo "Publishing XDM .NET Application..."
rm -rf build_output
mkdir -p build_output
dotnet publish app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o build_output/xdm-app
# Trimming is disabled in the csproj now, so it shouldn't strip GLibSharp types

echo "Creating RPM Package..."
RPM_BUILD_DIR="$(pwd)/rpmbuild"
rm -rf "$RPM_BUILD_DIR"
mkdir -p "$RPM_BUILD_DIR"/{BUILD,RPMS,SOURCES,SPECS,SRPMS}

cp -r build_output/xdm-app/* "$RPM_BUILD_DIR/SOURCES/"
cp deleted_files_folders/packaging-8.0.9-stale/deb/usr/share/applications/xdm-app.desktop "$RPM_BUILD_DIR/SOURCES/"

cat << 'EOF' > "$RPM_BUILD_DIR/SPECS/xdm.spec"
Name:           xdm
Version:        9.1.0
Release:        1%{?dist}
Summary:        Xtreme Download Manager
License:        GPLv2
%global __os_install_post %{nil}

%description
Xtreme Download Manager is a powerful tool to increase download speed, resume broken/dead downloads.

%prep

%build

%install
rm -rf $RPM_BUILD_ROOT
mkdir -p $RPM_BUILD_ROOT/opt/xdman
cp -a %{_sourcedir}/* $RPM_BUILD_ROOT/opt/xdman/
mkdir -p $RPM_BUILD_ROOT/usr/share/applications
cp %{_sourcedir}/xdm-app.desktop $RPM_BUILD_ROOT/usr/share/applications/xdm-app.desktop
mkdir -p $RPM_BUILD_ROOT/usr/share/pixmaps
cp $RPM_BUILD_ROOT/opt/xdman/xdm-logo-512.png $RPM_BUILD_ROOT/usr/share/pixmaps/xdm-app.png

%files
/opt/xdman/*
/usr/share/applications/xdm-app.desktop
/usr/share/pixmaps/xdm-app.png
EOF

cd "$RPM_BUILD_DIR"
rpmbuild --define "_topdir $RPM_BUILD_DIR" -bb SPECS/xdm.spec
cp RPMS/x86_64/xdm-9.1.0-1*.rpm "$OUT_DIR/"

echo "Done! Artifacts are in $OUT_DIR"
