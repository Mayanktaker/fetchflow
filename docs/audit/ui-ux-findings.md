# FetchFlow UI/UX Findings Register

<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->

Audit scope: GNOME (primary), KDE Plasma (primary), Cinnamon/GTK 3-only (compatibility).
Severity: **P0** data loss/crash/core task blocked · **P1** core journey impaired, inaccessible,
or desktop integration broken · **P2** usability/polish · **P3** refinement.

Status values: `open` · `fixed` · `accepted` (documented limitation) · `needs-decision`.

## Register

| ID | Sev | Desktop | Finding | Evidence | Status |
|:---|:---|:---|:---|:---|:---|
| FF-UI-001 | P1 | KDE, Cinnamon | The app forces `Gtk.Settings.Default.ThemeName = "Adwaita"` at startup and on every theme change, overriding the user's chosen GTK theme (Breeze on Plasma, Mint-Y on Cinnamon) for every widget the app CSS does not cover. | `Utils/ThemeManager.cs` (`AdwaitaThemeName` assignment in `ApplyTheme`) | needs-decision |
| FF-UI-002 | P1 | GNOME, KDE | Two notification transports: download-complete uses the D-Bus `org.freedesktop.Notifications` helper, while media-capture and generic notifications shell out to `notify-send`. On a session without that binary the second path fails silently. | `GtkPlatformUIService.ShowMediaNotification` / `ShowDesktopNotification` vs `Utils/DesktopNotificationHelper.cs` | fixed |
| FF-UI-003 | P1 | GNOME, KDE | No accessibility names anywhere in the GTK UI. Icon-only controls (main menu, speed limiter, theme toggle, update dot) are announced as unlabeled buttons by Orca/screen readers. | Zero `Atk`/`Accessible` references before this audit | fixed (toolbar/header); lists and dialogs still open |
| FF-UI-004 | P2 | GNOME, KDE | No keyboard accelerators exist. There is no `AccelGroup`, no `accelerator` entry in any Glade file, and no key handling outside the download lists, so core actions are pointer-only. | Zero accelerator matches across `.cs` + `glade/` | open |
| FF-UI-005 | P2 | GNOME | The app runs `Gtk.Application.Init` with a plain `Gtk.Window`, not `Gtk.ApplicationWindow` with registered `GSimpleAction`s. GNOME cannot surface app actions/menus and window/application lifetime is hand-managed. | `Program.cs` startup path | open |
| FF-UI-006 | P1 | all | `gtk_tree_view_get_bin_window` is P/Invoked directly from `libgtk-3.so.0`. Correct for the GTK 3 frontend, but it must never be compiled into shared or GTK 4 code. | `Utils/TreeViewSelectionHelper.cs` | open (GTK 4 blocker) |
| FF-UI-007 | P2 | GNOME, KDE | Download-row geometry is hard-coded in device pixels (row renderer paddings 10–16 px, gutter 52 px, header 52 px) rather than derived from font metrics, so row rhythm drifts at 150–200 % scaling and under large-font accessibility settings. | `MainWindow.cs` `DownloadRow*VerticalPadding`, `DownloadGutterWidth`; `docs/design.md` spacing section | open |
| FF-UI-008 | P1 | all | `GtkTreeView` + cell renderers are the core of the download lists and 12 Glade views. GTK 4 deprecates these APIs (since 4.10), so the list layer is the largest single porting cost. | 116 `TreeView` references across 11 C# files; 12 `GtkTreeView` + 12 `GtkTreeSelection` in Glade | open (GTK 4 blocker) |
| FF-UI-009 | P2 | GNOME, KDE | The base theme is applied to `Gdk.Screen.Default`, which GTK 4 no longer has (per-display style managers instead); theming must be re-plumbed for GTK 4 rather than reused. | `ThemeManager.ApplyTheme`, `Program.cs` CSS provider setup | open (GTK 4 blocker) |

## What is still unverified

These need a live session and are **not** claimed as findings yet:

- KDE Plasma: CSD vs SSD titlebar behaviour, Breeze contrast against the 14 app themes,
  tray/SNI rendering in the Plasma system tray, portal file-chooser behaviour.
- GNOME: headerbar button ordering against the platform default, notification
  appearance in the GNOME message tray, Orca navigation through the download list.
- Cinnamon/XFCE/MATE: GTK 3 compatibility run of the P0 journeys.
- Any backend-specific (Wayland vs X11) difference beyond the startup path already
  exercised by the smoke suite.

## Related

- `docs/audit/support-matrix.md` — environment matrix and recorded baseline
- `docs/audit/gtk4-coupling-inventory.md` — migration inventory
