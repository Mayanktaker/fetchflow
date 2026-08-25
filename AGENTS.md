# XDM — Agent Operating Map

<!-- © 2026 Mayanktaker Computers & Web Development | https://mayanktaker.com -->

Compact map for AI agents working on Xtreme Download Manager (Wayland Edition).
User-facing details live in [README.md](README.md); this file stays lean.

## Project environment

| Item | Value |
|------|-------|
| App | Xtreme Download Manager v9 — Wayland Edition |
| Stack | C# / .NET 8 (`net8.0`), GTK3 UI via Glade files |
| Runtime | .NET SDK 8.0.424 at `~/.dotnet8` (user-local, no sudo) |
| Browser extensions | Chrome + Firefox (vanilla JS, in `app/XDM/chrome-extension/` and `app/XDM/firefox-amo/`) |
| Build host | Fedora Linux x64 (bash); packaging tools: `rpmbuild`, `zip`, `tar` |
| rpmbuild setup | User-local extraction at `~/.local/rpm-build-root`; `~/.rpmmacros` sets `%_rpmconfigdir` there and `build_all.sh` adds it to `PATH` — RPM builds work without sudo |
| Version source of truth | `app/XDM/XDM.Linux.Installer/version.env` (currently `9.1.3`) |

## Key paths & commands

```bash
bash build_all.sh        # full release: extensions + tarball + rpm (+ deb/arch if tools present)
```

| Path | Purpose |
|------|---------|
| `app/XDM/XDM.Gtk.UI/` | Main GTK app (`net8.0`, publishes as self-contained single-file `linux-x64`) |
| `app/XDM/XDM.Core/` | Core download engine/library |
| `app/XDM/XDM.Linux.Installer/` | `make-rpm-pkg`, `make-deb-pkg`, `make-arch-pkg`, `version.env` |
| `xdm-release/` | Output folder for all release artifacts |

## Release rules

1. Run releases only when Mayank asks to **"generate release"** / **"build new release"**.
2. For a *new* release, bump the point version in `version.env` first (e.g. `9.1.2` → `9.1.3`), then run `build_all.sh`.
3. **Every release MUST include the `.rpm` artifact.** A failed or missing RPM build aborts `build_all.sh` with a hard error — do not ship without it (Fedora/RHEL is a first-class target). DEB/Arch failures only warn, with an "incomplete release" notice.
4. All artifacts land in project-root `xdm-release/`.
5. DEB and Arch packages additionally need `dpkg-deb` and `makepkg` (installable via `sudo dnf install -y dpkg pacman`).

## Conventions

- File header on all files: `© Mayanktaker Computers & Web Development | https://mayanktaker.com`
- Keep documentation single-source: detailed docs belong in dedicated `.md` files, linked from here — never duplicated.
