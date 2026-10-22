# FetchFlow Linux Support Matrix

<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->

Baseline evidence for the GNOME/KDE UI audit. Product direction: **GTK 4 is the
primary Linux UI; GTK 3 is the compatibility UI for desktops that only support GTK 3**
(see `docs/audit/gtk4-coupling-inventory.md`).

## Recorded baseline (2026-10-22)

| Item | Value |
|:---|:---|
| Host | CachyOS, Linux 7.3.0-2-cachyos, x86_64 |
| Session | GNOME, Wayland (`XDG_SESSION_TYPE=wayland`) |
| GTK 3 | 3.24.52 |
| GTK 4 | 4.24.1 |
| libadwaita | 1.10.0 |
| .NET SDK | 8.0.424 (`/mnt/Development/_DevSofts/dotnet8`) |
| GtkSharp | 3.24.24.38 (GTK 3 binding) |
| Build | `dotnet build app/XDM/XDM.Gtk.UI` → 0 errors, 1080 warnings (all pre-existing CA1416 platform warnings) |
| Tests | `XDM.Tests` → 153 passed, 0 failed, 0 skipped |

Only the GNOME/Wayland row is measured. Every other row is **unverified** until the
audit is run there — do not claim support from a row that has no evidence.

## Matrix

| Desktop | Backend | Frontend | GTK 4 available | Status | Notes |
|:---|:---|:---|:---|:---|:---|
| GNOME | Wayland | GTK 4 (primary) | yes | planned | Baseline measured on GTK 3 today |
| GNOME | X11 | GTK 4 (primary) | yes | planned | |
| KDE Plasma | Wayland | GTK 4 (primary) | yes | deferred | No Plasma session available (CachyOS GNOME); theme interaction was the main risk and is now addressed by not forcing the GTK theme name |
| KDE Plasma | X11 | GTK 4 (primary) | yes | deferred | No Plasma session available |
| Cinnamon | X11 | GTK 3 (compat) | depends on distro | planned | Primary GTK 3 target |
| XFCE / MATE | X11 | GTK 3 (compat) | usually GTK 3 only | planned | Second GTK 3 target |
| Other GTK 4 desktops (Sway, Hyprland, COSMIC) | Wayland | GTK 4 (primary) | yes | planned | Tray/SNI is the risk area |

## Per-environment capture checklist

For each row, record: app build/version, desktop + version, session type, display
scale, system theme and dark preference, portal version, tray/SNI availability,
screenshots of the main window and one dialog in both app themes, and the stderr
log of a clean start/quit.

## Related

- Findings: `docs/audit/ui-ux-findings.md`
- GTK 3→4 coupling inventory: `docs/audit/gtk4-coupling-inventory.md`
- Design tokens: `docs/design.md`
