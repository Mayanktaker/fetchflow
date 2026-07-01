#!/bin/bash
set -e

# Install flatpak builder if needed (requires sudo):
# sudo dnf install flatpak-builder

# Install .NET SDK extension
flatpak install -y flathub org.freedesktop.Sdk.Extension.dotnet8//23.08
flatpak install -y flathub org.freedesktop.Platform//23.08
flatpak install -y flathub org.freedesktop.Sdk//23.08

# Build the flatpak package locally
flatpak-builder --user --install --force-clean build-dir io.github.subhra74.xdm.yml

echo "Done! You can run the flatpak with: flatpak run io.github.subhra74.xdm"
