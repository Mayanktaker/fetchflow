<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->
# XDM on Wayland — User & Packager Notes

## Desktop Environment Support

| DE | Tray Icon | Notes |
|----|-----------|-------|
| KDE Plasma 6 | SNI (automatic) | Works out of the box |
| GNOME 42+ | SNI via extension | Requires [AppIndicator](#gnome-appindicator-extension) extension |
| Sway / Hyprland | SNI via waybar | Requires `status-notifier-watcher` or waybar SNI module |
| COSMIC | SNI (automatic) | Works out of the box |
| X11 (any DE) | Legacy XEmbed | Backward-compatible `Gtk.StatusIcon` fallback |
| GNOME (no extension) | None | Close → minimizes to taskbar + `notify-send` notification |

## GNOME AppIndicator Extension (Required for Tray on GNOME Wayland)

GNOME Shell does not provide a system tray host by default. To get the
XDM tray icon on GNOME Wayland, install the **AppIndicator and KStatusNotifierItem**
extension:

### Fedora (GNOME)
```bash
sudo dnf install gnome-shell-extension-appindicator
```
Then restart GNOME Shell (`Alt+F2` → `r` → Enter on X11; log out/in on Wayland)
or enable it in GNOME Extensions app.

### Ubuntu / Debian
```bash
sudo apt install gnome-shell-extension-appindicator
```

### Arch Linux
```bash
sudo pacman -S gnome-shell-extension-appindicator
```

### Manual install (any distro)
1. Visit https://extensions.gnome.org/extension/615/appindicator-support/
2. Toggle ON in the GNOME Extensions web interface
3. Log out and log back in

### Verify it's working
After installing and enabling the extension:
```bash
# Should return "true"
busctl --user get-property org.kde.StatusNotifierWatcher \
  /StatusNotifierWatcher org.kde.StatusNotifierWatcher \
  IsStatusNotifierHostRegistered
```

If the extension is not installed or disabled, XDM will still function —
it just won't show a tray icon. Close the window → app minimizes to taskbar;
use `Alt+Tab` or the taskbar entry to restore it.

## Wayland-Specific Behavior

### Close Button (X button)
- **With active downloads:** Minimizes to taskbar + `notify-send` notification.
  Restore via taskbar or `Alt+Tab`.
- **With no active downloads + tray icon active:** Hides to tray (classic behavior).
- **With no active downloads + no tray icon:** Exits cleanly.

### Window Positioning
XDM does not force center-alignment on Wayland (this causes GTK assertion
errors). The window manager controls placement on Wayland.

## Building & Packaging

### Requirements
- .NET 8.0 SDK (for building)
- .NET 8.0 runtime (for running framework-dependent packages)
- `dotnet-runtime-8.0` package on Fedora/RHEL

### Build
```bash
dotnet publish XDM.Gtk.UI/XDM.Gtk.UI.csproj \
  -c Release -r linux-x64 \
  --self-contained false \
  -p:PublishTrimmed=false
```

**Important:** Do NOT use `--self-contained true` or `-p:PublishTrimmed=true`.
Framework-dependent publish is required for correct GtkSharp/GLibSharp assembly
compatibility on glib2 ≥ 2.88.

### Package scripts
- `make-rpm-pkg` — Fedora/RHEL (requires `dotnet-runtime-8.0`)
- `make-deb-pkg` — Ubuntu/Debian
- `make-arch-pkg` — Arch Linux / AUR

All scripts source `version.env` for the version number and install:
- Hicolor scalable icon (`xdm-app.svg`)
- Freedesktop MIME package (`xdm-app.xml` for `application/xdm-app`)
- Post-install cache updates (`gtk-update-icon-cache`, `update-desktop-database`, `update-mime-database`)
