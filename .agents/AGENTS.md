# FetchFlow Download Manager — Project Rules

## Project Identity
- **App Name:** FetchFlow Download Manager (Wayland Edition)
- **App ID / Flathub:** `com.mayanktaker.fetchflow`
- **Developer / Maintainer:** Mayanktaker | Mayanktaker Computers & Web Development (https://mayanktaker.com)
- **App Version:** 9.1.4 (keep `AppInfo.cs`, `version.env`, and all `manifest.json` files in sync)
- **Copyright:** © 2013 - 2026 Mayanktaker | Mayanktaker Computers & Web Development

## Tech Stack
- **Core / Backend:** C# / .NET 8 (AOT-compatible where possible)
- **Desktop UI:** GTK3 via GtkSharp (`XDM.Gtk.UI`)
- **Browser Extension:** Manifest V3 (shared JS source in `chrome-extension/`, Firefox variant in `firefox-amo/`)
- **Video capture:** `yt-dlp` binary invoked via `YDLProcess`

## Architecture Overview

```
Extension (chrome/firefox)
    ↓ WebSocket IPC
IpcHttpMessageProcessor.cs     ← entry point for extension messages
    ↓
VideoUrlHelper.cs              ← media detection & yt-dlp orchestration
NetworkHelper.cs               ← shared hash/referer utilities
    ↓
IVideoTracker / VideoTracker   ← lifecycle of captured streams
    ↓
MediaGrabberWindow (GTK)       ← Video Downloader popup UI
```

## Key Rules

### Version Management
- **Single source of truth** for the Linux package version: `app/XDM/XDM.Linux.Installer/version.env`
- App internal version: `app/XDM/XDM.Core/AppInfo.cs` → `APP_VERSION`
- Browser extensions: `chrome-extension/manifest.json` and `firefox-amo/manifest.json`
- All three must be bumped together on every release.

### yt-dlp Multi-Site Capture
- `VideoUrlHelper.IsYdlSupportedUrl(url)` is the gatekeeper — add new domains to `SupportedYdlDomains` to extend capture.
- `ProcessMediaTab(url, tabId)` is the entry point — called from `IpcHttpMessageProcessor.OnTabUpdateMessage`.
- Results are cached for 5 minutes in `ydlCache` with automatic eviction via `ydlCacheEvictionTimer`.

### Events
- `IVideoTracker.MediaFetchStarted` fires before `yt-dlp` starts; `MediaFetchCompleted` fires when done (or on error).
- The GTK `MediaGrabberWindow` subscribes to these events and updates its title bar to `"(Fetching formats...)"` while loading.

### Copyright Header
- Every C# file touched in this project must have this as line 1:
  ```
  // © Mayanktaker Computers & Web Development | https://mayanktaker.com
  ```

### Packaging
- **Binary source:** Always use `build_output/xdm-app/` (underscore, ~72MB binary) as `binary-source/`. **Never** use `build-output/` (hyphen, smaller, older).
- **RPM:** Run `bash make-rpm-pkg` from `app/XDM/XDM.Linux.Installer/` after placing binaries in `binary-source/`. Output goes to `rpmbuild/RPMS/x86_64/`. Then copy to `xdm-release/`.
  - The spec uses `%define debug_package %{nil}` and `%global __strip /bin/true` — **do not remove these** or the binary will be stripped to ~11MB and the app won't run correctly.
  - The spec uses `AutoReqProv: no` + `Requires: gtk3 >= 3.24` only. **No `dotnet-runtime-8.0`** — the binary is a self-contained AOT build.
- **DEB:** Run `bash make-deb-pkg` from the same directory. Output is a `.deb` in that directory.
- **Chrome ZIP:** `cd chrome-extension && zip -r ../../../xdm-release/xdm-chrome-extension-<ver>.zip .`
- **Firefox XPI:** `cd firefox-amo && zip -r ../../../xdm-release/xdm-firefox-extension-<ver>.xpi .`
  - Firefox blocks unsigned XPIs in release builds. To install: use `about:debugging` → "Load Temporary Add-on", or use Firefox Developer Edition.
- All release artifacts are placed in `xdm-release/` at the repo root (gitignored — do not commit binaries).

### Release Generation (ALL THREE)

When the user says **"generate release"** or **"create release"**, produce all three targets below.
Use the version from `app/XDM/XDM.Linux.Installer/version.env` (currently `9.1.1`).

**Prerequisites:**
```bash
export DOTNET_ROOT=/home/mayanktakeroffice/.dotnet
export PATH="$DOTNET_ROOT:$PATH"
```

**Step 1 — Clean and prepare:**
```bash
rm -rf xdm-release build_output
mkdir -p xdm-release build_output
```

**Step 2 — Chrome Extension:**
```bash
cd app/XDM/chrome-extension
zip -r ../../../xdm-release/xdm-chrome-extension-<VER>.zip . -x ".*"
cd ../../../
```

**Step 3 — Firefox Extension:**
```bash
cd app/XDM/firefox-amo
zip -r ../../../xdm-release/xdm-firefox-extension-<VER>.xpi . -x ".*"
cd ../../../
```

**Step 4 — Fedora RPM + Portable Tarball:**
```bash
# Publish .NET app (self-contained, single-file, linux-x64)
dotnet publish app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=true -o build_output/xdm-app

# Portable tarball
cd build_output/xdm-app
tar -czf ../../xdm-release/xdm-linux-x64-<VER>.tar.gz *
cd ../../

# RPM
RPM_BUILD_DIR="$(pwd)/rpmbuild"
rm -rf "$RPM_BUILD_DIR"
mkdir -p "$RPM_BUILD_DIR"/{BUILD,RPMS,SOURCES,SPECS,SRPMS}
cp -r build_output/xdm-app/* "$RPM_BUILD_DIR/SOURCES/"
# (create spec — see build_rpm.sh for template, or run build_rpm.sh)
rpmbuild --define "_topdir $RPM_BUILD_DIR" -bb "$RPM_BUILD_DIR/SPECS/xdm.spec"
cp RPMS/x86_64/xdm-<VER>-1*.rpm xdm-release/
```

**Step 5 — Verify all artifacts exist:**
```bash
ls -lh xdm-release/
# Expected: chrome .zip, firefox .xpi, linux .tar.gz, .rpm
```

**Or simply run:**
```bash
bash build_all.sh    # does all of the above
# bash build_rpm.sh  # alternate: RPM + extensions only
```

### Never Do
- Never use hardcoded version strings — always pull from `APP_VERSION` / `version.env`.
- Never delete files — move to `deleted_files_folders/` instead.
- Do not use `yarn` or `pnpm` — use `npm` only.
- Never remove `%global __strip /bin/true` from the RPM spec — stripping breaks the AOT binary.
- Never add `Requires: dotnet-runtime-8.0` to the RPM spec — the binary is self-contained.
