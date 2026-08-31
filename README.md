<!-- © 2026 Mayanktaker Computers & Web Development | https://mayanktaker.com -->

<h1 align="center">FetchFlow Download Manager — Wayland Edition</h1>

<p align="center">
  <b>FetchFlow v9</b> — a powerful, fast download manager with native Wayland support,
  system tray integration, and seamless browser integration.
</p>

<p align="center">
  Maintained by <a href="https://mayanktaker.com">Mayanktaker Computers & Web Development</a>.
  Based on the original foundation of XDM by <a href="https://github.com/subhra74/xdm">Subhra Sankha Sarkar</a>.
</p>

---

## Highlights

- **Wayland-native** — runs on modern Wayland sessions (KDE Plasma, GNOME, Sway/Hyprland/COSMIC) as well as X11
- **System tray icon** with a right-click menu (Show / Quit) on KDE, GNOME + AppIndicator extension, waybar and other SNI hosts
- **Close-to-tray** — closing the window never quits the app; it keeps running in the background
- **Android-safe file names** — downloaded files keep only `A-Z a-z 0-9 . _ -` and space; everything else becomes `-`, so files copy cleanly to Android phones via USB/MTP
- **Browser integration** for Chrome, Firefox, Edge, Opera, Vivaldi and other Chromium/Firefox-based browsers
- **Video saving** from YouTube, Vimeo, Facebook, Instagram, Dailymotion and many more streaming sites
- **Faster downloads** with multi-connection acceleration and resume of broken/dead downloads
- **Modern packaging** — `.deb`, `.rpm`, Arch `.pkg.tar.zst`, portable `.tar.gz` and a Flatpak manifest (`com.mayanktaker.fetchflow`)

## What's New in FetchFlow v9

- Runs natively on **Wayland** (no XWayland quirks for window placement, dialogs or the tray icon).
- **System tray icon** on Wayland desktops via the StatusNotifier protocol, with a proper right-click menu on KDE Plasma 6.
- Closing the window now **hides the app to the tray** (or minimizes it) — FetchFlow always keeps running in the background. To fully quit, use the tray menu or the ☰ menu → Exit.
- **Blob downloads** (files generated in-page, e.g. streamed documents) are now captured by the browser extensions and transferred to FetchFlow.
- The **Firefox extension is faster and more reliable** — download takeovers no longer block pages, and right-click "Download with FetchFlow" works seamlessly.
- **Downloaded file names are cleaned automatically** — brackets, quotes, `%`, `^` and other characters that break copying to Android phones are replaced with `-`.
- **Video capture improved** for multi-site support with a loading indicator and smarter caching.
- Brand-new packaging pipeline: one command builds every distribution package.

## Features

- Increase download speeds by downloading file segments in parallel
- Resume broken or interrupted downloads
- Save videos from popular streaming websites
- Download web content directly from the browser with the right-click menu:
  - **Download with FetchFlow** — any link
  - **Download Image with FetchFlow** — images
  - **Download Blob Media with FetchFlow** — in-page media
- Schedule downloads, group them in queues, and limit download speed
- Categories with automatic folder assignment (documents, music, videos, programs, archives)
- Video conversion support via the built-in video downloader (yt-dlp based)
- Multi-language UI including Hindi, English and many more

## Installation

### Fedora / RHEL (and derivatives)

```bash
sudo dnf install ./fetchflow-9.1.4-1.fc44.x86_64.rpm
```

### Debian / Ubuntu (and derivatives)

```bash
sudo apt install ./fetchflow_9.1.4_amd64.deb
```

### Arch Linux / Manjaro / EndeavourOS

Build from the included `PKGBUILD` or install the prebuilt package:

```bash
makepkg -si   # from the XDM.Linux.Installer directory
# or
sudo pacman -U fetchflow-9.1.4-1-x86_64.pkg.tar.zst
```

### Portable (any modern x64 Linux)

```bash
tar -xzf fetchflow-linux-x64-9.1.4.tar.gz -C /opt
/opt/fetchflow/fetchflow
```

### Flatpak

A Flatpak manifest (`com.mayanktaker.fetchflow.yml`) is included for building your own Flatpak bundle:

```bash
flatpak-builder --user --install --force-clean build-dir com.mayanktaker.fetchflow.yml
```

> The app is installed under `/opt/fetchflow` and registers the `fetchflow://` URL scheme, the desktop entry and the tray icon automatically.

## Browser Integration

FetchFlow ships browser extensions for Chrome/Chromium and Firefox (MV3):

| Browser | Package | What it does |
|---|---|---|
| Chrome / Edge / Opera / Vivaldi | `fetchflow-chrome-extension-9.1.4.zip` | Takes over downloads, saves videos, adds context-menu items |
| Firefox | `fetchflow-firefox-extension-9.1.4.xpi` | Same features, tuned for Firefox's MV3 support |

The extension and the app talk over a **loopback relay** (`127.0.0.1:8597`, WebSocket first, HTTP fallback), and the OS-registered `fetchflow://` scheme launches FetchFlow on demand — no native messaging host needed on Linux.

To install:

1. Install the extension from the Chrome Web Store / Firefox Add-ons, or load the packaged file manually (Firefox: `about:debugging` → Load Temporary Add-on; Chrome: `chrome://extensions` → Developer mode → Load unpacked).
2. Start FetchFlow once — it registers the URL scheme and browser monitoring automatically (first run shows the integration dialog).
3. Enable monitoring with the browser-monitor toggle in the main window.

Right-click any link and choose **Download with FetchFlow**.

## System Tray & Window Close

- Closing the window **hides FetchFlow to the tray** when a tray icon is available; otherwise the window minimizes and FetchFlow keeps running.
- Tray detection retries in the background for ~2 minutes at startup, so a tray host that starts after FetchFlow (e.g. plasmashell at login) is picked up automatically.
- **Quit** is only available from the tray icon's right-click menu or the ☰ menu → Exit. If downloads are in progress, FetchFlow asks for confirmation first.

## File Naming (Android / MTP safe)

Downloaded files keep only letters, digits, `.`, `_`, `-` and spaces. All other characters (`( ) [ ] { } % ^ # " ' : ? * < > |` etc.) are replaced with `-` automatically, and repeated dashes are collapsed — so `My [file] (1)^.mp4` becomes `My -file- 1.mp4`. This applies to all new downloads and keeps files copying to Android phones, TVs and NAS devices without errors.

## Building from Source

### Requirements

- .NET SDK 8 (the build script expects it under `~/.dotnet8` or on `PATH`)
- Linux x64 host with GTK3 development libraries
- `zip` for packaging the browser extensions

### Build everything

```bash
bash build_all.sh
```

All artifacts land in `xdm-release/`:

| Artifact | Format | Target |
|---|---|---|
| `fetchflow-linux-x64-<ver>.tar.gz` | Portable tarball | Any modern x64 Linux |
| `fetchflow_<ver>_amd64.deb` | DEB | Debian / Ubuntu |
| `fetchflow-<ver>-1.x86_64.rpm` | RPM | Fedora / RHEL / openSUSE |
| `fetchflow-<ver>-1-x86_64.pkg.tar.zst` | Arch package | Arch / Manjaro / EndeavourOS |
| `fetchflow-chrome-extension-<ver>.zip` | Chrome extension | Chrome Web Store |
| `fetchflow-firefox-extension-<ver>.xpi` | Firefox extension | Firefox Add-ons (AMO) |

The version is defined once in `app/XDM/XDM.Linux.Installer/version.env` — all packaging scripts source it, so bump it there before a release.

### Run the tests

```bash
dotnet test app/XDM/XDM.Tests/XDM.Tests.csproj
```

## Data & Configuration

| Item | Location |
|---|---|
| Downloads database, queues, settings | `~/.fetchflow-app-data/` (`downloads.db`, `queues.db`, `settings.dat`) |
| Legacy fallback migration | `~/.xdm-app-data/` (auto-detected and migrated seamlessly) |
| Log file (debug mode) | `~/.fetchflow-app-data/log.txt` |
| Installed app | `/opt/fetchflow/` |
| Auto-start entry | `~/.config/autostart/fetchflow.desktop` |

## Credits & License

**FetchFlow Download Manager (Wayland Edition)** is maintained by [Mayanktaker Computers & Web Development](https://mayanktaker.com), © 2026.

The project builds upon the foundational architecture of the original **Xtreme Download Manager**, originally created by [Subhra Sankha Sarkar (subhra74)](https://github.com/subhra74/xdm).

This project is licensed under the **GNU General Public License v2** — see [`LICENSE`](LICENSE).

