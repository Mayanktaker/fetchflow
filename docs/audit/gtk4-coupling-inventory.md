# GTK 3 → GTK 4 Coupling Inventory

<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->

Migration inventory for the Linux UI. Product direction: **GTK 4 is the primary
Linux frontend; GTK 3 stays as the compatibility frontend** for desktops that only
ship GTK 3 (Cinnamon, XFCE, MATE, older distro releases).

Measured 2026-10-22 against `app/XDM/XDM.Gtk.UI` (49 C# files, 30 Glade files, 14 theme CSS files).

## What is coupled to GTK 3

| Area | Volume | GTK 4 status | Approach |
|:---|:---|:---|:---|
| `Gtk.TreeView` / models / cell renderers | 116 references in 11 C# files | deprecated since 4.10 | Replace with `GtkListView`/`GtkColumnView` + `GListModel` in the GTK 4 frontend. This is the largest single work item; port it first behind the vertical slice. |
| Glade-built dialogs | 30 files (12 `GtkTreeView`, 12 `GtkTreeSelection`, 5 `GtkMenu`, 5 `GtkNotebook`, 5 `GtkListBoxRow`) | mostly still loadable; cell-view/dialog widgets partly deprecated | Rebuild the high-traffic dialogs as GTK 4 Builder files. Do not attempt to load the GTK 3 Glade set unchanged. |
| Theme CSS | 14 files, GTK 3 node selectors (`treeview`, `headerbar`, `frame > border`, `entry selection`) | GTK 4 CSS node tree differs; `@define-color`/`linear-gradient` fine, node names not | Keep the GTK 3 themes as-is for the compatibility frontend; author a separate GTK 4 stylesheet from the same tokens in `docs/design.md`. |
| Base theme plumbing | `Gdk.Screen.Default`, `StyleContext.AddProviderForScreen` (4 sites) | no `Gdk.Screen` in GTK 4 | Re-plumb to `Gtk.StyleContext.AddProviderForDisplay` per display. |
| Native P/Invoke | `libgtk-3.so.0` `gtk_tree_view_get_bin_window` | not applicable | GTK 3 frontend only. Must never be referenced from shared code. |
| GTK-level settings | `Gtk.Settings.Default.ThemeName` / `ApplicationPreferDarkTheme` (4 sites) | renamed APIs (`Gtk.Settings.GtkThemeName`, `gtk-application-prefer-dark-theme`) | Re-implement in the GTK 4 frontend; see FF-UI-001 for the KDE theme-respect decision. |
| Menus / popovers | 37 sites | `GtkMenu` removed in favour of `GtkPopoverMenu` | Port to `GMenu` + `GtkPopoverMenu`. |
| `Gdk.Window` event routing | 1 site (`TreeViewSelectionHelper`) | removed concept in GTK 4 | Disappears with the `TreeView` replacement. |

## Frontend split

```
app/XDM/
  XDM.Core/            shared domain + services (no GTK types)
  XDM.Messaging/       shared
  XDM.Compatibility/   shared
  XDM.Gtk.UI/          GTK 3 frontend  (compatibility)
  XDM.Gtk4.UI/         GTK 4 frontend  (primary)   <- to be created after the spike
```

Rules for the split:

- No GTK 3 or GTK 4 types in shared projects. Presentation policies that are pure
  logic (sorting, row geometry tokens, tray policy, color scheme table) stay shared.
- The GTK 4 frontend must not depend on the GTK 3 assembly, and vice versa. No
  in-process GTK 3/GTK 4 runtime switch.
- libadwaita is **optional GNOME polish, never a hard requirement**: a Plasma user
  must get a first-class GTK 4 window without it.

## Launch / package policy (to be validated in the spike)

1. Ship the GTK 4 frontend as the default `fetchflow` launcher on GTK 4-capable
   desktops.
2. Ship the GTK 3 frontend as a separate entry point (`fetchflow-gtk3` +
   `com.mayanktaker.fetchflow.gtk3.desktop`) that GTK 3-only desktops install or
   select.
3. Do not pick the frontend by guessing the desktop name at runtime. Use tested
   packaging (dependency/desktop-entry choice) or an explicit user setting.
4. The GTK 3 package must stay installable with no GTK 4 runtime dependency.

## GTK 4 vertical slice (spike gate)

Must work before any porting work is committed to:

- app startup and shutdown on GNOME/Wayland and Plasma/Wayland
- main window with headerbar, sidebar, and one large download list
  (selection, multi-select, sort, progress)
- one representative modal dialog
- dark/light plus system preference behaviour
- file chooser through the portal
- a package that declares only the runtimes it actually needs

## Risks

| Risk | Mitigation |
|:---|:---|
| .NET GTK 4 binding maturity (GtkSharp targets GTK 3.22; GirCore is the alternative) | Spike both before committing; treat binding choice as a gate, not a preference |
| List rewrite changes selection/sort behaviour that took many tests to stabilise | Port the existing behavioural tests to the GTK 4 list before swapping the UI |
| Two frontends drift apart | Shared policies + per-frontend smoke tests; parity checklist per release |
| Theme work doubles | Single token source (`docs/design.md`), two generated-by-hand stylesheets |
| GTK 3 users stranded | GTK 3 frontend stays a build + release target until an explicit deprecation decision |

## Related

- `docs/audit/support-matrix.md`
- `docs/audit/ui-ux-findings.md`
- `docs/design.md`
