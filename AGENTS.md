# FetchFlow — Agent Operating Map

<!-- © 2026 Mayanktaker Computers & Web Development | https://mayanktaker.com -->

Compact operating map for AI agents working on FetchFlow Download Manager (Wayland Edition).
User-facing details live in [README.md](README.md); design system tokens live in [docs/superpowers/specs/2026-08-30-xdm-design-system-modernization-design.md](docs/superpowers/specs/2026-08-30-xdm-design-system-modernization-design.md).

## Project Environment

| Dimension | Specification | Details / Version |
|:---|:---|:---|
| **App Name & ID** | FetchFlow Download Manager (Wayland Edition) | App ID: `com.mayanktaker.fetchflow` |
| **Backend & Core Engine** | C# / .NET 8 (`net8.0`) | AOT-compiled self-contained single-file binary |
| **Desktop UI** | GTK3 via GtkSharp | `XDM.Gtk.UI` with Glade XML templates & live CSS theming |
| **Database** | SQLite | Local SQLite database via `System.Data.SQLite` / `SQLite.Interop.dll` |
| **Video Extraction** | `yt-dlp` CLI wrapper | Multi-site stream extraction orchestrated by `VideoUrlHelper.cs` |
| **Browser Extensions** | Manifest V3 (Vanilla JS) | Chrome (`app/XDM/chrome-extension/`), Firefox (`app/XDM/firefox-amo/`) |
| **Runtime & Toolchain** | .NET SDK 8.0.424 | Local at `~/.dotnet8` (or `~/.dotnet`); no root/sudo needed |
| **Packaging Tools** | `rpmbuild`, `dpkg-deb`, `zip`, `tar` | User-local rpmbuild at `~/.local/rpm-build-root` (`%_rpmconfigdir`) |
| **Version Source of Truth** | `app/XDM/XDM.Linux.Installer/version.env` | Currently `9.1.4` (synced with `AppInfo.cs` and manifests) |

## Related Documentation & Single Sources of Truth

| Document | Purpose & Scope |
|:---|:---|
| [`README.md`](README.md) | User-facing documentation, installation guide, and feature overview |
| [`CHROMEWEBSTORE.md`](CHROMEWEBSTORE.md) | Chrome Web Store listing metadata, permission justifications & 2026 Limited Use policies |
| [`docs/privacy.html`](docs/privacy.html) | HTTPS Privacy Policy certifying zero telemetry, zero ad tracking, and local loopback IPC |
| [`.github/workflows/release.yml`](.github/workflows/release.yml) | Automated CI/CD release workflow for tarballs, RPM, DEB, extensions & SHA256SUMS |
| [`docs/superpowers/specs/2026-08-30-xdm-design-system-modernization-design.md`](docs/superpowers/specs/2026-08-30-xdm-design-system-modernization-design.md) | UI/UX design tokens, GTK styling rules, and layout specifications |

## Key Commands

| Command | Purpose |
|:---|:---|
| `bash build_all.sh` | Build complete release: Chrome ZIP, Firefox XPI, portable tarball, RPM, DEB & SHA256SUMS |
| `dotnet test app/XDM/XDM.Tests -c Release` | Run 20-test automated suite (MSTest, YDL parser, JSON, Glade wiring) |
| `scripts/run-gtk-smoke.sh` | Run headless GTK builder/autoconnect smoke tests under virtual Xvfb display |

## Release & Store Compliance Rules (2026)

1. **Release Trigger:** Run releases only when Mayank asks to **"generate release"** / **"build new release"**, or push a git tag (`v*`) to trigger `.github/workflows/release.yml`.
2. **Version Bump:** For new releases, bump `version.env` first (e.g. `9.1.4` → `9.1.5`), then sync `AppInfo.cs` and `manifest.json` files.
3. **Mandatory Artifacts:** Every release **MUST** produce the Fedora `.rpm` package and `SHA256SUMS.txt` inside `xdm-release/`.
4. **Chrome Web Store (CWS):** Managed via `CHROMEWEBSTORE.md`. YouTube stream capture is disabled on CWS store builds to prevent policy bans; direct GitHub/website releases retain full multi-site capture.
5. **Firefox AMO:** Requires `"data_collection_permissions": { "required": ["none"] }` in `app/XDM/firefox-amo/manifest.json`. All JS is unminified vanilla JS.
6. **Diagnostics:** Always-on rotating crash log at `~/.fetchflow-app-data/crash.log` (capped at 5 MB). Glade UI wiring regressions tested via `GladeWiringTests`.
7. **Conventions:** File header: `© Mayanktaker Computers & Web Development | https://mayanktaker.com`. Never hardcode versions or delete files directly.
