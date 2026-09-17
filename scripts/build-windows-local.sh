#!/bin/bash
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
set -e

# Resolve .NET SDK root location
if [ -x "/mnt/Development/_DevSofts/dotnet8/dotnet" ]; then
    export DOTNET_ROOT=/mnt/Development/_DevSofts/dotnet8
elif [ -x "/home/mayanktakeroffice/.dotnet8/dotnet" ]; then
    export DOTNET_ROOT=/home/mayanktakeroffice/.dotnet8
elif [ -d "$HOME/.dotnet8" ]; then
    export DOTNET_ROOT="$HOME/.dotnet8"
fi
export PATH=$DOTNET_ROOT:$PATH

# Verify required build dependencies
for tool in dotnet zip; do
    command -v "$tool" >/dev/null 2>&1 || { echo "ERROR: required tool '$tool' not found in PATH." >&2; exit 1; }
done

# Resolve version from single source of truth
source app/XDM/XDM.Linux.Installer/version.env
echo "=== Building FetchFlow Windows Release v${VERSION} ==="

# Pre-release automated test gate
echo "=== Running pre-release verification tests ==="
dotnet build app/XDM/XDM.Tests/XDM.Tests.csproj -c Release
"$DOTNET_ROOT/dotnet" app/XDM/XDM.Tests/bin/Release/net8.0/XDM.Tests.dll || {
    echo "ERROR: Test suite failed. Aborting Windows release build." >&2
    exit 1
}

# Pre-release extension blocklist parity check
if command -v node >/dev/null 2>&1; then
    node scripts/test-noise-filter.mjs || {
        echo "ERROR: noise-filter parity test failed. Aborting Windows release build." >&2
        exit 1
    }
fi

# Set up clean output directories
WIN_DIR="build_output/xdm-win-x64"
OUT_DIR="$(pwd)/fetchflow-release"
mkdir -p "$WIN_DIR"
mkdir -p "$OUT_DIR"

# Build WPF main UI targeting .NET Framework 4.7.2
echo "=== Compiling XDM.Wpf.UI (fetchflow.exe) ==="
dotnet build -c Release -f net4.7.2 app/XDM/XDM.Wpf.UI/XDM.Wpf.UI.csproj -o "$WIN_DIR"

# Build WinForms browser integration guide targeting .NET Framework 4.7.2
echo "=== Compiling XDM.WinForms.IntegrationUI (xdm-guide.exe) ==="
dotnet build -c Release -f net4.7.2 app/XDM/XDM.WinForms.IntegrationUI/XDM.WinForms.IntegrationUI.csproj -o "$WIN_DIR"

# Sync browser extensions
echo "=== Synchronizing browser extensions ==="
rm -rf "$WIN_DIR/chrome-extension" "$WIN_DIR/firefox-extension"
cp -r app/XDM/chrome-extension "$WIN_DIR/chrome-extension"
cp -r app/XDM/firefox-amo "$WIN_DIR/firefox-extension"

# Sync brand assets and logos
echo "=== Synchronizing brand assets ==="
cp app/XDM/fetchflow-logo.ico "$WIN_DIR/xdm-logo.ico"
cp app/XDM/fetchflow-logo.svg "$WIN_DIR/fetchflow-logo.svg"
cp app/XDM/fetchflow-logo-512.png "$WIN_DIR/fetchflow-logo-512.png"

# Ensure images folder has new FetchFlow logos
mkdir -p "$WIN_DIR/images"
cp app/XDM/XDM.Wpf.UI/images/fetchflow-logo*.png "$WIN_DIR/images/" 2>/dev/null || :

# Package portable Windows zip archive
ZIP_NAME="fetchflow-windows-x64-portable-${VERSION}.zip"
echo "=== Packaging Portable Windows ZIP: ${ZIP_NAME} ==="
cd "$WIN_DIR"
# Exclude debug symbols (.pdb) from release zip
zip -r "$OUT_DIR/${ZIP_NAME}" . -x "*.pdb"
cd ../..

# Generate/update SHA256 Checksums
echo "=== Updating SHA-256 Checksums ==="
cd "$OUT_DIR"
touch SHA256SUMS.txt
# Remove any prior entry for this zip then append fresh
sed -i "/${ZIP_NAME}/d" SHA256SUMS.txt 2>/dev/null || true
sha256sum "$ZIP_NAME" >> SHA256SUMS.txt
cd ..

echo "=========================================================="
echo "Windows Portable Release successfully created!"
echo "Package: $OUT_DIR/${ZIP_NAME}"
ls -lh "$OUT_DIR/${ZIP_NAME}"
echo "=========================================================="
