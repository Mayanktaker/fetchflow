<!-- © 2026 Mayanktaker Computers & Web Development | https://mayanktaker.com -->

<h1 align="center">Xtreme Download Manager — Wayland Edition</h1>

<p align="center">
  <b>XDM v9</b> — a powerful download manager with native Wayland support,
  system tray integration, and seamless browser integration.
</p>

<p align="center">
  Based on the original <b>Xtreme Download Manager</b> by
  <a href="https://github.com/subhra74/xdm">Subhra Sankha Sarkar (subhra74)</a>.
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
- **Modern packaging** — `.deb`, `.rpm`, Arch `.pkg.tar.zst`, portable `.tar.gz` and a Flatpak manifest

## What's New in v9

- Runs natively on **Wayland** (no XWayland quirks for window placement, dialogs or the tray icon).
- **System tray icon** on Wayland desktops via the StatusNotifier protocol, with a proper right-click menu on KDE Plasma 6.
- Closing the window now **hides the app to the tray** (or minimizes it) — XDM always keeps running in the background. To fully quit, use the tray menu or the ☰ menu → Exit.
- **Blob downloads** (files generated in-page, e.g. streamed documents) are now captured by the browser extensions and transferred to XDM.
- The **Firefox extension is faster and more reliable** — download takeovers no longer block pages, and right-click "Download with XDM" works even when XDM is closed (the extension launches XDM and queues the download).
- **Downloaded file names are cleaned automatically** — brackets, quotes, `%`, `^` and other characters that break copying to Android phones are replaced with `-`.
- **Video capture improved** for multi-site support with a loading indicator and smarter caching.
- Brand-new packaging pipeline: one command builds every distribution package.

## Features

- Increase download speeds by downloading file segments in parallel
- Resume broken or interrupted downloads
- Save videos from popular streaming websites
- Download web content directly from the browser with the right-click menu:
  - **Download with XDM** — any link
  - **Download Image with XDM** — images
  - **Download Blob Media with XDM** — in-page media
- Schedule downloads, group them in queues, and limit download speed
- Categories with automatic folder assignment (documents, music, videos, programs, archives)
- Video conversion support via the built-in video downloader (yt-dlp based)
- Multi-language UI including Hindi, English and many more

## Screenshots

Main window:

![XDM main window](docs/panel.png)

## Installation

### Debian / Ubuntu (and derivatives)

```bash
sudo apt install ./xdman_gtk_9.1.3_amd64.deb
```

### Fedora / RHEL (and derivatives)

```bash
sudo dnf install ./xdm-9.1.3-1.x86_64.rpm
```

### Arch Linux / Manjaro / EndeavourOS

Build from the included `PKGBUILD` or install the prebuilt package:

```bash
makepkg -si   # from the XDM.Linux.Installer directory
# or
sudo pacman -U xdm-9.1.3-1-x86_64.pkg.tar.zst
```

### Portable (any modern x64 Linux)

```bash
tar -xzf xdm-linux-x64-9.1.3.tar.gz -C /opt
/opt/xdman/xdm-app
```

### Flatpak

A Flatpak manifest (`io.github.subhra74.xdm.yml`) is included for building your own Flatpak bundle.

> The app is installed under `/opt/xdman` and registers the `xdm-app://` URL scheme, the desktop entry and the tray icon automatically.

## Browser Integration

XDM ships browser extensions for Chrome/Chromium and Firefox (MV3):

| Browser | Package | What it does |
|---|---|---|
| Chrome / Edge / Opera / Vivaldi | `xdm-chrome-extension-9.1.3.zip` | Takes over downloads, saves videos, adds context-menu items |
| Firefox | `xdm-firefox-extension-9.1.3.xpi` | Same features, tuned for Firefox's MV3 support |

The extension and the app talk over a **loopback relay** (`127.0.0.1:8597`, WebSocket first, HTTP fallback), and the OS-registered `xdm-app://` scheme launches XDM on demand — no native messaging host needed on Linux.

To install:

1. Install the extension from the Chrome Web Store / Firefox Add-ons, or load the packaged file manually (Firefox: `about:debugging` → Load Temporary Add-on; Chrome: `chrome://extensions` → Developer mode → Load unpacked).
2. Start XDM once — it registers the URL scheme and browser monitoring automatically (first run shows the integration dialog).
3. Enable monitoring with the browser-monitor toggle in the main window.

Right-click any link and choose **Download with XDM**.

## System Tray & Window Close

- Closing the window **hides XDM to the tray** when a tray icon is available; otherwise the window minimizes and XDM keeps running.
- Tray detection retries in the background for ~2 minutes at startup, so a tray host that starts after XDM (e.g. plasmashell at login) is picked up automatically.
- **Quit** is only available from the tray icon's right-click menu or the ☰ menu → Exit. If downloads are in progress, XDM asks for confirmation first.

See [`app/XDM/XDM.Linux.Installer/WAYLAND.md`](app/XDM/XDM.Linux.Installer/WAYLAND.md) for detailed Wayland notes.

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
| `xdm-linux-x64-<ver>.tar.gz` | Portable tarball | Any modern x64 Linux |
| `xdman_gtk_<ver>_amd64.deb` | DEB | Debian / Ubuntu |
| `xdm-<ver>-1.x86_64.rpm` | RPM | Fedora / RHEL / openSUSE |
| `xdm-<ver>-1-x86_64.pkg.tar.zst` | Arch package | Arch / Manjaro / EndeavourOS |
| `xdm-chrome-extension-<ver>.zip` | Chrome extension | Chrome Web Store |
| `xdm-firefox-extension-<ver>.xpi` | Firefox extension | Firefox Add-ons (AMO) |

The version is defined once in `app/XDM/XDM.Linux.Installer/version.env` — all packaging scripts source it, so bump it there before a release.

### Run the tests

```bash
dotnet test app/XDM/XDM.Tests/XDM.Tests.csproj
```

## Data & Configuration

| Item | Location |
|---|---|
| Downloads database, queues, settings | `~/.xdm-app-data/` (`downloads.db`, `queues.db`, `settings.dat`) |
| Log file (debug mode) | `~/.xdm-app-data/log.txt` |
| Installed app | `/opt/xdman/` |
| Auto-start entry | `~/.config/autostart/xdm-app.desktop` |

## Credits & License

**Xtreme Download Manager (XDM) Wayland Edition** is maintained by [Mayanktaker Computers & Web Development](https://mayanktaker.com), © 2026.

The project is a continuation of the original **Xtreme Download Manager**, created by [Subhra Sankha Sarkar (subhra74)](https://github.com/subhra74/xdm) and his contributors. Huge thanks to the original author and the open-source community that built XDM.

This project is licensed under the **GNU General Public License v2** — see [`LICENSE`](LICENSE).
