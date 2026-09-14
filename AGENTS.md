# FetchFlow — Agent Operating Map

<!-- © 2026 Mayanktaker Computers & Web Development | https://mayanktaker.com -->

Operating map for AI agents. User docs: [README.md](README.md). Design tokens: [docs/design.md](docs/design.md).

## Environment

| Stack | Detail |
|:---|:---|
| Core | C# / .NET 8 (`net8.0`), AOT single-file binary; Windows UI on .NET Framework 4.7.2 (`XDM.Wpf.UI`, `XDM.WinForms.IntegrationUI`) |
| Linux UI | GTK3 via GtkSharp (`XDM.Gtk.UI`, Glade XML + live CSS) |
| Windows UI | WPF `XDM.Wpf.UI` at GTK feature parity since 9.1.15.5: themes via `Utils/WpfThemeManager.cs` (live swap), Checksum + Import/Export dialogs, theme/scheme settings. Scheme data single-sourced in `XDM.Core/UI/ColorSchemeTable.cs` (both UIs consume it — keep hex tokens in sync there only) |
| Database | SQLite (`System.Data.SQLite`), DB at `~/.fetchflow-app-data/downloads.db`, crash log `crash.log` (5 MB cap) |
| Video | `yt-dlp` CLI wrapper via `VideoUrlHelper.cs` |
| Extensions | Manifest V3 vanilla JS: `app/XDM/chrome-extension/`, `app/XDM/firefox-amo/` (shared `noise-filter.js` twin + core `NetworkHelper.cs` — keep all 3 blocklists identical) |
| Toolchain | .NET SDK 8.0.424 at `~/.dotnet8`; `rpmbuild`, `dpkg-deb`, `zip`, `tar`; no root/sudo. WPF cannot build on Linux — `xdm-wpf-build.yml` (windows-latest) is its only build gate |
| Version | `app/XDM/XDM.Linux.Installer/version.env` — currently `9.1.15.5` (sync `AppInfo.cs` + both `manifest.json` + WPF `<AssemblyVersion>` + `.iss` `AppVersion` default) |

## Docs (don't duplicate, point here)

`README.md` (user/install) · `CHROMEWEBSTORE.md` (CWS listing + Limited Use) · `docs/privacy.html` (zero-telemetry policy) · `.github/workflows/release.yml` + `sync-gh-pages.yml` + `xdm-wpf-build.yml` (CI/release) · `docs/design.md` (UI tokens)

## Commands

| Command | Purpose |
|:---|:---|
| `bash build_all.sh` | Full Linux release (test gate → ZIP, XPI, tarball, RPM, DEB, SHA256SUMS) |
| `dotnet app/XDM/XDM.Tests/bin/Release/net8.0/XDM.Tests.dll` | Full automated suite (MSTest console runner) |
| `node scripts/test-noise-filter.mjs` | Blocklist parity: chrome/firefox/core lists + junk vectors (also in build gate) |
| `scripts/run-gtk-smoke.sh` | Headless GTK smoke under Xvfb |
| `scripts/cleanup-junk-captures.sh` | Purge junk captures (quit app first; auto-backup) |

## Rules

1. Releases only on Mayank's "generate release"/"build new release" or a `v*` tag.
2. Version bump order: `version.env` → `AppInfo.cs` → `manifest.json` files.
3. Every release MUST ship: `.rpm` + `.deb` + `.tar.gz` + Windows setup/ZIP + `.zip`/`.xpi` + `SHA256SUMS.txt` in `fetchflow-release/`. Windows artifacts come from `xdm-wpf-build.yml`: `fetchflow-windows-x64-<ver>-setup.exe`, `fetchflow-windows-x64-portable-<ver>.zip`, `SHA256SUMS-windows-<ver>.txt` (tag attach only).
4. CWS builds disable YouTube stream capture (policy); GitHub builds keep full capture. AMO needs `"data_collection_permissions": {"required": ["none"]}`; all extension JS unminified.
5. File header: `© Mayanktaker Computers & Web Development | https://mayanktaker.com`. Never hardcode versions or delete files directly.
6. Stream captures (`videoplayback`, `.m3u8`, `.mpd`) route to extension menu (`VideoTracker`), not desktop dialogs; web assets (`image/*`, `_next/image`) never auto-capture.
