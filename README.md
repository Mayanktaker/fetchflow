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
  <img src="https://img.shields.io/badge/Platform-Linux%20%7C%20Wayland%20%7C%20X11%20%7C%20Windows-brightgreen?style=flat-square" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-8.0%20AOT-512BD4?style=flat-square&logo=dotnet" alt=".NET 8" />
  <img src="https://img.shields.io/badge/UI-GTK3%20CSD%20%7C%20Windows%20WPF-4A90E2?style=flat-square" alt="UI" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-GPL--2.0-blue?style=flat-square" alt="License" /></a>
  <a href="https://mayanktaker.com"><img src="https://img.shields.io/badge/Maintained%20by-Mayanktaker-F97316?style=flat-square" alt="Maintainer" /></a>
</p>

<p align="center">
  <a href="https://mayanktaker.github.io/fetchflow/"><b>🌐 Official Website</b></a> &bull;
  <a href="https://github.com/Mayanktaker/fetchflow/releases"><b>📦 Downloads &amp; Releases</b></a> &bull;
  <a href="#-browser-extensions-manifest-v3"><b>🧩 Browser Extensions</b></a> &bull;
  <a href="#-why-fetchflow-comparison"><b>⚖️ Comparison</b></a> &bull;
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

## ✨ What's New in 9.1.14

**What's new**
- Firefox extension now stays connected reliably and captures downloads the moment they start, with a live connection-health pill and a one-click Test Capture button in the popup
- Download lists gain click-to-select checkboxes, drag-to-select, and right-click that respects your multi-selection, with the delete confirmation showing exactly how many items will go
- Fresh installs save into their own `Downloads/FetchFlow` folder with tidy subfolders (Videos, Music, Documents, Compressed, Programs, Pictures) instead of mixing with your other files
- New default looks: Nord Emerald for dark mode, Nordic Frost for light mode
- Hovering a download highlights it clearly; the details line (location, website, date) is smaller and quieter so file names stand out

**Bug fixes**
- Fixed downloads list sometimes deleting only one item when several were selected
- Fixed media downloads with no cookies silently failing to appear
- Fixed the app occasionally spawning extra background copies of itself

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

### Windows 11 / 10 (64-bit)
- **Standalone Setup Wizard:** Download [`fetchflow-windows-x64-setup.exe`](https://github.com/Mayanktaker/fetchflow/releases/latest) from Releases for complete desktop integration, start menu shortcuts, and auto-start persistence.
- **Portable ZIP:** Download [`fetchflow-windows-x64-9.1.14.zip`](https://github.com/Mayanktaker/fetchflow/releases/latest), extract to any folder, and run `fetchflow.exe` with zero installation required.

### Fedora / RHEL / CentOS / openSUSE (RPM)
```bash
sudo dnf install https://github.com/Mayanktaker/fetchflow/releases/latest/download/fetchflow-9.1.14-1.fc44.x86_64.rpm
```

### Debian / Ubuntu / Linux Mint / Pop!_OS (DEB)
```bash
sudo apt install ./fetchflow_9.1.14_amd64.deb
```

### Arch Linux / Manjaro / EndeavourOS
```bash
# Using prebuilt package:
sudo pacman -U https://github.com/Mayanktaker/fetchflow/releases/latest/download/fetchflow-9.1.14-1-x86_64.pkg.tar.zst

# Or build via PKGBUILD:
cd app/XDM/XDM.Linux.Installer && makepkg -si
```

### Universal Portable Tarball
```bash
tar -xzf fetchflow-linux-x64-9.1.14.tar.gz -C /opt/
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
| **Chromium** | Google Chrome, Brave, Microsoft Edge, Opera, Vivaldi | `fetchflow-chrome-extension-9.1.14.zip` | One-click takeover, context-menu download, in-page blob media capture, video bar, <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>F</kbd> shortcut |
| **Gecko** | Mozilla Firefox, Floorp, LibreWolf, Waterfox | `fetchflow-firefox-extension-9.1.14.xpi` | Background streaming listener, seamless takeover, media sniffing, <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>F</kbd> shortcut |

### Manual Installation
- **Chrome / Chromium:** Open `chrome://extensions` &rarr; Toggle *Developer mode* &rarr; Click *Load unpacked* &rarr; Select `app/XDM/chrome-extension`.
- **Firefox:** Open `about:debugging#/runtime/this-firefox` &rarr; Click *Load Temporary Add-on* &rarr; Select `app/XDM/firefox-amo/manifest.json`.

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
- Packaging tools: `rpmbuild`, `tar`, `zip` (optional: `dpkg-deb`, `makepkg`)

### Build Everything (Binaries + Packages + Extensions)
```bash
bash build_all.sh
```

All compiled packages land in `fetchflow-release/`.

### Run Automated Tests
```bash
dotnet test app/XDM/XDM.Tests/XDM.Tests.csproj
```

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

## 📜 Credits & License

- **Developer & Maintainer:** [Mayanktaker Computers & Web Development](https://mayanktaker.com)
- **Open-Source Heritage:** Built upon the original XDM foundation by [Subhra Sankha Sarkar](https://github.com/subhra74/xdm).
- **License:** [GNU General Public License v2.0 (GPL-2.0)](LICENSE)
