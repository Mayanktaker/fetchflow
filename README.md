<!-- © 2026 Mayanktaker Computers & Web Development | https://mayanktaker.com -->

<p align="center">
  <a href="https://mayanktaker.github.io/fetchflow/">
    <img src="docs/fetchflow-logo.png" width="128" height="128" alt="FetchFlow Download Manager Logo" />
  </a>
</p>

<h1 align="center">FetchFlow Download Manager</h1>

<p align="center">
  <b>High-Performance Multi-Stream Download Accelerator & Media Ingestion Engine</b>
  <br />
  <i>Native Wayland &bull; Modern GTK3 CSD &bull; .NET 8 AOT &bull; System Tray &bull; Manifest V3 Browser Integration</i>
</p>

<p align="center">
  <a href="https://github.com/Mayanktaker/fetchflow/releases/latest"><img src="https://img.shields.io/github/v/release/Mayanktaker/fetchflow?color=orange&style=flat-square&logo=github" alt="Latest Release" /></a>
  <a href="https://github.com/Mayanktaker/fetchflow/releases"><img src="https://img.shields.io/github/downloads/Mayanktaker/fetchflow/total?color=blue&style=flat-square" alt="Total Downloads" /></a>
  <a href="https://addons.mozilla.org/en-US/firefox/addon/fetchflow-browser-helper/"><img src="https://img.shields.io/badge/Firefox_Add--on-FetchFlow_Helper-FF7139?style=flat-square&logo=firefox-browser" alt="Firefox Add-on" /></a>
  <img src="https://img.shields.io/badge/Platform-Linux%20%7C%20Wayland%20%7C%20X11%20%7C%20Windows-brightgreen?style=flat-square" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-8.0%20AOT-512BD4?style=flat-square&logo=dotnet" alt=".NET 8" />
  <img src="https://img.shields.io/badge/UI-GTK3%20CSD%20%7C%20Windows%20WPF-4A90E2?style=flat-square" alt="UI" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-GPL--2.0-blue?style=flat-square" alt="License" /></a>
  <a href="#-support--donate-via-paypal"><img src="https://img.shields.io/badge/Donate-PayPal-00457C?style=flat-square&logo=paypal" alt="Donate via PayPal" /></a>
  <a href="https://mayanktaker.com"><img src="https://img.shields.io/badge/Maintained%20by-Mayanktaker-F97316?style=flat-square" alt="Maintainer" /></a>
</p>

<p align="center">
  <a href="https://mayanktaker.github.io/fetchflow/"><b>🌐 Official Website</b></a> &bull;
  <a href="https://github.com/Mayanktaker/fetchflow/releases"><b>📦 Downloads &amp; Releases</b></a> &bull;
  <a href="https://addons.mozilla.org/en-US/firefox/addon/fetchflow-browser-helper/"><b>🦊 Firefox Add-on</b></a> &bull;
  <a href="#-browser-extensions-manifest-v3"><b>🧩 Browser Extensions</b></a> &bull;
  <a href="#-reviews--feedback"><b>⭐ Reviews &amp; Feedback</b></a> &bull;
  <a href="#-support--donate-via-paypal"><b>☕ Donate</b></a> &bull;
  <a href="https://github.com/Mayanktaker/fetchflow/issues"><b>🐛 Report an Issue</b></a>
</p>

---

## ⚡ Key Capabilities

| Capability | Engineering Detail |
|---|---|
| **Parallel Stream Chunking** | Dynamic multi-socket segmentation (up to 32 parallel connections per file) with real-time chunk re-assembly |
| **Native Wayland Architecture** | Zero XWayland dependency; native surface allocation on KDE Plasma 6, GNOME 46+, Sway, Hyprland, and COSMIC |
| **StatusNotifierItem (SNI) Tray** | Full D-Bus system tray menu with real-time download speed display, remaining ETA, and background persistence |
| **Bandwidth Throttle & Limiter** | Live toolbar and bottom bar speed limiter presets (50 KB/s to 5 MB/s, custom) to prevent network saturation |
| **Audio Notification Chimes** | Customizable download completion chime with one-click toggle in main menu and settings |
| **14 Curated Color Themes** | 7 Dark and 7 Light refined palettes with full JSON palette import and export support |
| **Collapsible Category Badges** | Interactive category sidebar with live download item counter badges and collapsible sections |
| **Integrated Media Grabber** | Built-in `yt-dlp` stream extraction engine supporting video/audio ingestion from 1,000+ streaming sites |
| **Manifest V3 Browser Addons** | High-speed local loopback IPC (port `8597`) with global shortcut (<kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>F</kbd>) and blob capture |
| **Multi-Language Desktop UI** | Fully localized interface with Hindi, Hinglish, English, Spanish, French, German, Russian, Chinese, Arabic, and flag indicators |
| **Android / MTP Sanitizer** | Automatic sanitization of illegal characters (`: ? * " < > \| ( ) [ ] ^ %`) for seamless USB file transfers |

---

## ✨ What's New in 9.1.15.18 — Reimagined Download List

**What's new**
- **Drag any column to size it your way:** the downloads list header is a real column header — pull the edge between the selection tick, file name and size, and the headings move with the columns. A divider marks the grab point.
- **A list that breathes:** taller GTK4-style header, more air between downloads, roomier rows — on Linux and Windows alike.
- **Cohesive, modern surfaces:** buttons, menus, popovers and cards now share one look — matching borders in a row, flat GTK4-style corners, and hovers you can actually see.
- **Themes that stick:** your theme and color scheme now persist across restarts, with Charcoal Blue (dark) and Classic Blue (light) as the new defaults.

**Bug fixes**
- Fixed selecting multiple downloads with the tick box (ticks now accumulate, untick, and work on every row).
- Fixed the list header bar disappearing on light themes and columns no longer being resizable.
- Fixed selected rows turning header-coloured when FetchFlow is unfocused, and the tick box becoming invisible with them.
- Fixed the Color Scheme dropdown opening empty, and theme/palette choices silently resetting on every launch.
- Fixed the FetchFlow icon missing from the GNOME dock and taskbar.

---

## ✨ What's New in 9.1.15.17 — Maintenance

**What's new**
- **Cleaner upgrade on Windows:** if an older version left a startup shortcut behind, FetchFlow now clears it on first launch so the app no longer starts twice at login.

**Bug fixes**
- Fixed the download page on the website occasionally lagging one release behind.

---

## ✨ What's New in 9.1.15.16 — Autostart & System Tray Fixes

**What's new**
- **Real icon in the system tray:** FetchFlow now shows its own orange download icon on GNOME, KDE, and Wayland panels instead of a generic three-dot placeholder. The icon installs itself, so it also appears correctly on installs made before this fix.
- **Launch at login that actually works:** FetchFlow reliably starts in the background when you sign in, and automatically repoints itself to the current install location after an update or reinstall.

**Bug fixes**
- Fixed the "Start FetchFlow when I sign in" option not switching launch-at-login off when unticked.
- Fixed the system tray showing three dots instead of the FetchFlow icon.
- Fixed launch-at-login silently failing after a reinstall while Settings still showed it as switched on.

---

## ✨ What's New in 9.1.15.15 — Modernized Windows UI & Parity

**What's new (Windows & Cross-Platform)**
- **Integrated CSD Header Bar**: Sleek client-side decorated title bar with real-time Sun/Moon theme toggle button directly in the window caption.
- **Dynamic Category View Subtitle**: Window title bar dynamically reflects your active download view (e.g. `· All Unfinished`, `· All Finished`, `· Videos`).
- **Elevated Card-Style Download Rows**: Modern rounded cards with 6px border radius, alternating surface tints, smooth 120ms hover animations, and 1px accent active selection borders.
- **Capsule Pill Search Input**: Full-capsule search bar with integrated quick search button matching modern design systems.
- **Softened Dark Scheme Contrast**: Replaced pitch-black borders and surfaces with comfortable, refined `#2A3F3A` borders and `#131C1A` dark background for improved readability.
- **Windows Package Manager (Winget) Manifests**: Added support for automated Windows package installation via Winget (`winget install Mayanktaker.FetchFlow`).
- **Complete Visual Parity**: All controls, dialogs, buttons, and layouts now share an identical modern design language across both Linux and Windows.

**Bug fixes**
- Fixed maximize window boundary overflow under the Windows taskbar.
- Fixed selection highlight borders in download lists to track active color schemes smoothly.
- Fixed window close button behavior to consistently minimize to system tray.

---

## ⚖️ Why FetchFlow? (Comparison)

| Feature / Metric | FetchFlow v9 | Internet Download Manager (IDM) | Free Download Manager (FDM) | Wget / cURL |
|---|:---:|:---:|:---:|:---:|
| **Platform Support** | Linux (Wayland/X11), Windows | Windows Only | Windows, macOS, Linux | Cross-Platform (CLI) |
| **Native Wayland CSD** | **✓ Full Native** | ✗ No (Windows Only) | ✗ Partial (XWayland) | ✗ CLI Only |
| **Stream Acceleration** | **✓ Up to 32 Sockets** | ✓ Yes | ✓ Yes | ✗ Single-stream |
| **Video Sniffing & yt-dlp** | **✓ Built-in (1000+ Sites)** | ✓ Proprietary | ✗ Limited | ✗ No |
| **Browser Extensions** | **✓ Manifest V3** | ✓ MV3 | ✓ MV3 | ✗ No |
| **License & Cost** | **✓ 100% Free & FOSS (GPL-2.0)** | ✗ Commercial ($24.95) | ✗ Proprietary Core | ✓ Open Source |
| **Resource Footprint** | **✓ ~35 MB RAM (AOT Binary)** | ~40 MB RAM | ~120 MB RAM (Electron/Qt) | ~10 MB RAM |

---

## 📦 Installation & Packaging

FetchFlow distributes self-contained binaries for **Windows 11 / 10** and **Linux** with zero runtime prerequisites.

### Universal Installer (Recommended — CachyOS / Manjaro / Arch / Fedora / Debian / openSUSE)
```bash
curl -fsSL https://github.com/Mayanktaker/fetchflow/releases/latest/download/install-fetchflow.sh | bash
# Options: bash install-fetchflow.sh --version 9.1.15.18 --yes --check --uninstall
```
The script auto-detects your distro, installs system deps (`gtk3`, `ffmpeg`, `xdg-desktop-portal`), then installs the native package (`.pkg.tar.zst` on Arch-family, `.rpm` on Fedora, `.deb` on Debian/Ubuntu) with a portable `/opt/fetchflow` tarball fallback.

### CachyOS / Manjaro / Arch / EndeavourOS / Garuda
```bash
# Via AUR helper (simplest on Arch-family):
yay -S fetchflow-bin
# or: paru -S fetchflow-bin

# Via universal installer (handles deps + fallback automatically):
curl -fsSL https://github.com/Mayanktaker/fetchflow/releases/latest/download/install-fetchflow.sh | bash

# Or manually with the prebuilt package (download first:
# direct-URL pacman needs a .sig we don't publish yet):
sudo pacman -S --needed yt-dlp
curl -fsSL https://github.com/Mayanktaker/fetchflow/releases/latest/download/fetchflow-9.1.15.18-1-x86_64.pkg.tar.zst -o /tmp/fetchflow.pkg.tar.zst
sudo pacman -U /tmp/fetchflow.pkg.tar.zst
```
Package signatures (`.sig`) ship once signing is configured — see [docs/signing.md](docs/signing.md).

### Windows 11 / 10 (64-bit)
- **Windows Package Manager (Winget):**
  ```powershell
  winget install Mayanktaker.FetchFlow
  ```
- **Standalone Setup Wizard:** Download [`fetchflow-windows-x64-setup.exe`](https://github.com/Mayanktaker/fetchflow/releases/latest) from Releases for complete desktop integration, start menu shortcuts, and auto-start persistence.
- **Portable ZIP:** Download [`fetchflow-windows-x64-portable-9.1.15.18.zip`](https://github.com/Mayanktaker/fetchflow/releases/latest), extract to any folder, and run `fetchflow.exe` with zero installation required.

### Fedora / RHEL / CentOS / openSUSE (RPM)
```bash
sudo dnf install https://github.com/Mayanktaker/fetchflow/releases/latest/download/fetchflow-9.1.15.18-1.fc44.x86_64.rpm
```

### Debian / Ubuntu / Linux Mint / Pop!_OS (DEB)
```bash
sudo apt install ./fetchflow_9.1.15.18_amd64.deb
```

### Arch Linux / Manjaro / EndeavourOS (legacy manual PKGBUILD)
```bash
# Or build via PKGBUILD:
cd app/XDM/XDM.Linux.Installer && makepkg -si
```

### Universal Portable Tarball
```bash
tar -xzf fetchflow-linux-x64-9.1.15.18.tar.gz -C /opt/
/opt/fetchflow/fetchflow
```

### Flatpak
```bash
flatpak-builder --user --install --force-clean build-dir com.mayanktaker.fetchflow.yml
```

---

## 🧩 Browser Extensions (Manifest V3)

FetchFlow includes native Manifest V3 browser extensions with zero cloud telemetry:

| Browser Family | Supported Browsers | Package | Features |
|---|---|---|---|
| **Chromium** | Google Chrome, Brave, Microsoft Edge, Opera, Vivaldi | `fetchflow-chrome-extension-9.1.15.18.zip` | One-click takeover, context-menu download, in-page blob media capture, video bar, <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>F</kbd> shortcut |
| **Gecko** | Mozilla Firefox, Floorp, LibreWolf, Waterfox | [Firefox Add-ons (AMO)](https://addons.mozilla.org/en-US/firefox/addon/fetchflow-browser-helper/) · [Direct XPI](https://github.com/Mayanktaker/fetchflow/releases/latest/download/fetchflow-firefox-extension-9.1.15.18.xpi) | Background streaming listener, seamless takeover, media sniffing, <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>F</kbd> shortcut |


### Installation
- **Firefox:** Install directly with 1 click from **[Firefox Add-ons (AMO)](https://addons.mozilla.org/en-US/firefox/addon/fetchflow-browser-helper/)**, or download the standalone [.xpi release](https://github.com/Mayanktaker/fetchflow/releases/latest/download/fetchflow-firefox-extension-9.1.15.18.xpi).
- **Chrome / Chromium:** Open `chrome://extensions` &rarr; Toggle *Developer mode* &rarr; Click *Load unpacked* &rarr; Select `app/XDM/chrome-extension`.

---

## 🏗️ Architecture Overview

```
Browser Extension (Chrome / Firefox MV3)
    │
    │ (WebSocket / HTTP Loopback IPC @ 127.0.0.1:8597)
    ▼
IpcHttpMessageProcessor.cs
    ├── VideoUrlHelper.cs  (yt-dlp media extraction & format caching)
    ├── NetworkHelper.cs   (Header parsing, referer injection, checksums)
    └── DownloadEngine.cs  (Parallel socket chunking & multi-part assembler)
            ▼
GTK3 Modern Shell (XDM.Gtk.UI)
    ├── Modern CSD HeaderBar & Responsive Paned Sidebar
    ├── StatusNotifierItem (D-Bus Tray Client for KDE/GNOME/Waybar)
    └── SQLite Storage Engine (~/.fetchflow-app-data/downloads.db)
```

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| <kbd>Ctrl</kbd> + <kbd>N</kbd> | Open New Download Dialog |
| <kbd>Ctrl</kbd> + <kbd>V</kbd> | Open Video Downloader (Media Grabber) |
| <kbd>Ctrl</kbd> + <kbd>B</kbd> | Open Batch Download Dialog |
| <kbd>Ctrl</kbd> + <kbd>Q</kbd> | Exit FetchFlow Completely |
| <kbd>Delete</kbd> | Delete Selected Download Entry |
| <kbd>Shift</kbd> + <kbd>Delete</kbd> | Delete Download and Delete File from Disk |
| <kbd>F5</kbd> | Refresh Download List & Active Speeds |
| <kbd>Ctrl</kbd> + <kbd>,</kbd> | Open Settings & Preferences |
| <kbd>Alt</kbd> + <kbd>Shift</kbd> + <kbd>F</kbd> | Open Browser Extension Media & Download Panel |

---

## 🔧 Building from Source

### Prerequisites
- .NET SDK 8.0 (`net8.0`)
- GTK3 development libraries (`gtk3`, `glib2`, `cairo`, `pango`)
- Packaging tools: `tar`, `zip` (optional: `dpkg-deb`, `makepkg`)

> **Note on `rpmbuild`:** `build_all.sh` requires it, because every release ships an `.rpm`.
> It is **not packaged for Arch/Manjaro** and is not in the AUR, so a local full release build
> is not possible on Arch-family systems. Either push a `v*` tag and let GitHub Actions cut the
> release, or run `build_all.sh` on Fedora (`dnf install rpm-build`). For a local Arch-family
> install of just the Arch package, use `./scripts/rebuild-install.sh`.

### Build Everything (Binaries + Packages + Extensions)
```bash
bash build_all.sh
```

All compiled packages land in `fetchflow-release/`.

### Quick Dev Loop (Arch-family: Manjaro / CachyOS / Arch)
Build and install straight from source — runs the test suite, publishes the binary, builds the
Arch package, installs it with `pacman`, then repairs the launch-at-login entry:
```bash
./scripts/rebuild-install.sh               # build + install (asks for your sudo password)
./scripts/rebuild-install.sh --no-install  # build only, no sudo
```
This is a development loop only — for a full release matrix use `build_all.sh`.

### Run Automated Tests
```bash
dotnet test app/XDM/XDM.Tests/XDM.Tests.csproj
```

### Lint Shell Scripts
```bash
shellcheck -S warning scripts/*.sh build_all.sh xdm-updater.sh app/XDM/XDM.Linux.Installer/make-arch-pkg app/XDM/XDM.Linux.Installer/make-deb-pkg app/XDM/XDM.Linux.Installer/make-rpm-pkg
```
Runs automatically on every push via `.github/workflows/lint.yml`.

---

## 📁 System Configuration & Diagnostic Paths

| File / Folder | Standard Location | Flatpak / Sandbox Location |
|---|---|---|
| **Database & Metadata** | `~/.fetchflow-app-data/downloads.db` | `$XDG_CONFIG_HOME/fetchflow/downloads.db` |
| **Queues Configuration** | `~/.fetchflow-app-data/queues.db` | `$XDG_CONFIG_HOME/fetchflow/queues.db` |
| **Crash & Diagnostic Log** | `~/.fetchflow-app-data/crash.log` (5 MB auto-rotated) | `$XDG_CONFIG_HOME/fetchflow/crash.log` |
| **Installation Directory** | `/opt/fetchflow/` | `/app/bin/` |

### 🛟 Single-Instance Self-Recovery

FetchFlow allows only one running instance. If a previous instance ever becomes unresponsive (its internal message port stops answering), the next launch detects this within a couple of seconds and automatically takes over as the primary instance — the app always starts instead of silently exiting. The IPC listener is also supervised and rebinds itself if it ever stops unexpectedly, keeping browser-extension connectivity alive.

---

## ⭐ Reviews & Feedback

Your feedback powers FetchFlow's continuous refinement! If FetchFlow has accelerated your downloads or simplified media ingestion:

- **Leave a Review on Firefox Add-ons (AMO):** If you use the Firefox integration, please take 30 seconds to rate us and share your feedback on the **[FetchFlow AMO Review Page](https://addons.mozilla.org/en-US/firefox/addon/fetchflow-browser-helper/reviews/)**. It helps other users discover high-performance, telemetry-free downloading.
- **Star & Share the Repository:** A ⭐ on GitHub helps boost visibility across the open-source community.
- **Feature Requests & Bug Reports:** Have an idea for improvement, new video platform support, or UI enhancements? Open a discussion or ticket on **[GitHub Issues](https://github.com/Mayanktaker/fetchflow/issues)**.

---

## ☕ Support & Donate via PayPal

FetchFlow is **100% free, open-source, zero-telemetry, and ad-free software**. We build and maintain high-performance native desktop clients and browser integrations entirely independently.

If FetchFlow brings value to your daily workflow, consider supporting ongoing development, hosting, and test hardware:

<p align="center">
  <a href="https://www.paypal.com/donate/?business=mayanktaker_hell%40yahoo.co.in&currency_code=USD" target="_blank" rel="noopener">
    <img src="https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif" alt="Donate with PayPal" />
  </a>
</p>

<p align="center">
  <b>PayPal Account:</b> <code>mayanktaker_hell@yahoo.co.in</code>
  <br />
  <a href="https://www.paypal.com/donate/?business=mayanktaker_hell%40yahoo.co.in&currency_code=USD"><b>Click here to donate via PayPal &rarr;</b></a>
</p>

---

## 📜 Credits & License

- **Developer & Maintainer:** [Mayanktaker Computers & Web Development](https://mayanktaker.com)
- **Open-Source Heritage:** Built upon the original XDM foundation by [Subhra Sankha Sarkar](https://github.com/subhra74/xdm).
- **License:** [GNU General Public License v2.0 (GPL-2.0)](LICENSE)
