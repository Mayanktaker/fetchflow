# FetchFlow Download Manager

Fast, modern open-source download accelerator and video downloader for **Linux (Wayland & X11)** and **Windows**.

FetchFlow accelerates downloads using multi-connection segments, captures streaming video, and
integrates natively with Wayland desktops and modern browsers. It is self-contained: no separate
runtime, no installer prompts, no telemetry.

**Based on the excellent work of the [XDM](https://github.com/subhra74/xdm) project.**

## Download

Head to the [project site](https://mayanktaker.github.io/fetchflow/) for one-click downloads of
Linux (`.rpm`, `.deb`, Arch `.pkg.tar.zst`, portable `.tar.gz`) and Windows (installer `.exe`,
portable `.zip`) packages, plus the Chrome MV3 extension and the Firefox `.xpi`.

One-line installer (auto-detects your distro):

```bash
curl -fsSL https://github.com/Mayanktaker/fetchflow/releases/latest/download/install-fetchflow.sh | bash
```

## Verify your download

Every release ships a `SHA256SUMS.txt`. Check it with:

```bash
sha256sum -c SHA256SUMS.txt
```

## Features

- Multi-connection segmented downloading with pause, resume and retry
- Streaming video capture via the bundled browser extension
- `yt-dlp` integration for audio and video extraction from 1000+ sites
- Native Wayland support, plus X11
- Live GNOME, KDE, and Wayland system tray with a proper branded icon
- Launch-at-login that survives updates and reinstalls
- Dark and light themes, RTL layout, and 12 interface languages
- Zero telemetry — nothing is collected or transmitted

## Links

- Source: https://github.com/Mayanktaker/fetchflow
- Releases: https://github.com/Mayanktaker/fetchflow/releases
- Privacy policy: https://mayanktaker.github.io/fetchflow/privacy.html
- Signatures: see `docs/signing.md` in the source repository

## License

GNU GPL v2 — see the source repository for the full text. XDM is licensed under GPL v2 as well.
