<!-- © 2026 Mayanktaker Computers & Web Development | https://mayanktaker.com -->

<p align="center">
  <img src="docs/fetchflow-logo.png" width="120" height="120" alt="FetchFlow Download Manager Logo" />
</p>

<h1 align="center">FetchFlow Download Manager</h1>

<p align="center">
  <b>High-Performance Multi-Stream Download Accelerator & Media Ingestion Engine</b>
  <br />
  <i>Native Wayland &bull; Modern GTK3 CSD &bull; .NET 8 AOT &bull; System Tray &bull; Browser Integration</i>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Linux%20%7C%20Wayland%20%7C%20X11-orange?style=flat-square" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-8.0%20(AOT)-512BD4?style=flat-square&logo=dotnet" alt=".NET 8" />
  <img src="https://img.shields.io/badge/UI-GTK3%20CSD-4A90E2?style=flat-square&logo=gtk" alt="GTK3" />
  <img src="https://img.shields.io/badge/License-GPL--2.0-blue?style=flat-square" alt="License" />
  <img src="https://img.shields.io/badge/Maintained%20by-Mayanktaker-F97316?style=flat-square" alt="Maintainer" />
</p>

<p align="center">
  Maintained with ❤️ by <a href="https://mayanktaker.com"><b>Mayanktaker Computers &amp; Web Development</b></a>.
  <br />
  <i>Built on the open-source foundation of XDM by <a href="https://github.com/subhra74/xdm">Subhra Sankha Sarkar</a>.</i>
</p>

---

## ⚡ Key Highlights

| Feature | Technical Capability |
|---|---|
| **Multi-Stream Acceleration** | Divides downloads into dynamic parallel segments (up to 32 connections) for maximum throughput |
| **Native Wayland Support** | Full Wayland client integration (KDE Plasma 6, GNOME 46+, Sway, Hyprland, COSMIC) with zero XWayland quirks |
| **System Tray Integration** | StatusNotifierItem (SNI) D-Bus protocol on Wayland with a full right-click context menu (Show / Hide / Quit) |
| **Video & Stream Grabber** | Built-in streaming media detection powered by `yt-dlp` — save video/audio from YouTube, Vimeo, Twitch, and 1000+ sites |
| **Browser Integration** | Vanilla Manifest V3 extensions for Chrome, Firefox, Brave, Edge, Opera, Vivaldi, and Floorp |
| **Android / MTP Safe Names** | Automatically sanitizes downloaded file names to ASCII-safe formats, avoiding transfer failures over USB/MTP |
| **Resilient Resumption** | Seamlessly resume broken, timed-out, or expired downloads with automatic URL refresh and token re-acquisition |
| **Modern Packaging** | First-class distribution packages: Fedora/RHEL (`.rpm`), Debian/Ubuntu (`.deb`), Arch (`.pkg.tar.zst`), Flatpak, and Portable Tarball |

---

## 📦 Installation & Packaging

FetchFlow distributes self-contained, high-performance Linux binaries that require no separate runtime installation.

### Fedora / RHEL / CentOS (RPM)
```bash
sudo dnf install ./xdm-release/fetchflow-9.1.4-1.fc44.x86_64.rpm
```

### Debian / Ubuntu / Linux Mint (DEB)
```bash
sudo apt install ./xdm-release/fetchflow_9.1.4_amd64.deb
```

### Arch Linux / Manjaro / EndeavourOS
```bash
# Install via prebuilt package
sudo pacman -U ./xdm-release/fetchflow-9.1.4-1-x86_64.pkg.tar.zst

# Or build via PKGBUILD
cd app/XDM/XDM.Linux.Installer && makepkg -si
```

### Portable Tarball (Universal Linux x64)
```bash
tar -xzf xdm-release/fetchflow-linux-x64-9.1.4.tar.gz -C /opt/
/opt/fetchflow/fetchflow
```

### Flatpak
```bash
flatpak-builder --user --install --force-clean build-dir com.mayanktaker.fetchflow.yml
```

---

## 🌐 Browser Extensions (Manifest V3)

FetchFlow integrates directly with your default web browser via native WebSocket/HTTP loopback IPC:

| Browser Family | Extension Artifact | Features |
|---|---|---|
| **Chromium** (Chrome, Brave, Edge, Opera, Vivaldi) | `fetchflow-chrome-extension-9.1.4.zip` | Automatic download interception, in-page blob media capture, video sniffing, context menu |
| **Gecko** (Firefox, Floorp, LibreWolf, Waterfox) | `fetchflow-firefox-extension-9.1.4.xpi` | Full MV3 support, background streaming listener, right-click download triggers |

### Loading Extensions Manually
- **Chrome / Chromium:** Navigate to `chrome://extensions` &rarr; Enable *Developer mode* &rarr; Click *Load unpacked* and select `app/XDM/chrome-extension`.
- **Firefox:** Navigate to `about:debugging#/runtime/this-firefox` &rarr; Click *Load Temporary Add-on* and select `app/XDM/firefox-amo/manifest.json`.

---

## 🛠️ Architecture & Core Engine

```
Browser Extension (MV3)
    │ (WebSocket / HTTP Loopback IPC @ 127.0.0.1:8597)
    ▼
IpcHttpMessageProcessor
    ├── VideoUrlHelper (yt-dlp stream extraction & caching)
    ├── NetworkHelper (Connection pooling & multi-socket segmentation)
    └── DownloadEngine (Segment assembler & chunk verification)
            ▼
GTK3 Modern Shell (Wayland CSD HeaderBar & Responsive Paned TreeView)
    │
    ├── StatusNotifierItem (D-Bus Tray Client for KDE/GNOME/Waybar)
    └── SQLite Storage Engine (~/.fetchflow-app-data/downloads.db)
```

---

## 🔧 Building from Source

### Prerequisites
- .NET SDK 8.0 (`net8.0`)
- GTK3 runtime & development libraries (`gtk3`, `glib2`, `cairo`, `pango`)
- Standard build tools: `tar`, `zip`, `rpmbuild` (optional: `dpkg-deb`, `makepkg`)

### One-Command Release Build
```bash
bash build_all.sh
```

This compiles the self-contained single-file AOT binary, packages all Linux distribution targets (RPM, DEB, Arch, Tarball), and bundles the browser extensions into `xdm-release/`.

### Running Automated Test Suite
```bash
dotnet test app/XDM/XDM.Tests/XDM.Tests.csproj
```

---

## 📁 Data & Configuration Paths

| Purpose | Default Path (Standard Linux) | Flatpak / Sandbox Path |
|---|---|---|
| **Database & Settings** | `~/.fetchflow-app-data/downloads.db` | `$XDG_CONFIG_HOME/fetchflow/downloads.db` |
| **Queues Configuration** | `~/.fetchflow-app-data/queues.db` | `$XDG_CONFIG_HOME/fetchflow/queues.db` |
| **Crash & Diagnostic Log** | `~/.fetchflow-app-data/crash.log` (5 MB rotated) | `$XDG_CONFIG_HOME/fetchflow/crash.log` |
| **Binary Installation** | `/opt/fetchflow/` | `/app/bin/` |
| **Desktop Entry & Icons** | `/usr/share/applications/fetchflow.desktop`<br>`/usr/share/icons/hicolor/scalable/apps/fetchflow.svg` | App package metadata |

---

## 📜 Credits & License

- **Lead Developer & Maintainer:** [Mayanktaker Computers & Web Development](https://mayanktaker.com)
- **Original Architecture & Heritage:** [Subhra Sankha Sarkar (subhra74)](https://github.com/subhra74/xdm)
- **License:** [GNU General Public License v2.0 (GPL-2.0)](LICENSE)
