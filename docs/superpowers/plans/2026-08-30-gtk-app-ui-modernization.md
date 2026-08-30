# XDM GTK App UI Modernization — Implementation Plan (Phases 1–2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Modernize the GTK main window and dialogs per the approved design: rounded corners, spacing scale, HeaderBar with themed window controls, modern sidebar with count badges, refined two-line download list — zero feature loss, indistinguishable performance.

**Architecture:** All changes live in the presentation layer (`MainWindow.cs`, `Utils/GtkHelper.cs`, `theme/*.css`, glade files). Lists stay `Gtk.TreeView`; the sidebar becomes a `Gtk.ListBox` in a `Gtk.Paned`; window controls move to a CSD `Gtk.HeaderBar`. `XDM.Core` is untouched.

**Tech Stack:** C# / .NET 8, GtkSharp 3.24.24.38, GTK3 CSS theming, Glade XML.

**Spec:** `docs/superpowers/specs/2026-08-30-xdm-design-system-modernization-design.md` — read it before starting; this plan argues from it.

## Global Constraints

- .NET SDK lives at `~/.dotnet8` — build with `~/.dotnet8/dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj` from repo root.
- File header comment on every touched file: `© Mayanktaker Computers & Web Development | https://mayanktaker.com` (already present — keep it).
- GTK3 CSS has **no variables** — token values are inlined and documented in each theme file's header comment block, mirrored between `xdm-dark.css` and `xdm-light.css`.
- Every new user-visible string goes through `TextResource.GetText(...)` (i18n) — never hardcode English.
- No new NuGet packages. No new timers. No changes in `XDM.Core`, `XDM.Messaging`, `XDM.Compatibility`.
- UI verification is build + launch + visual (GTK widgets need a display; there is no UI test harness). Regression gate: `~/.dotnet8/dotnet test app/XDM/XDM.Tests/XDM.Tests.csproj` must stay green.
- Commit after every task. Conventional commit messages, ≤50-char subject.
- TreeView per-row selection may fall back to rectangular highlight if rounded rows render poorly (pre-approved in spec §10).

---

### Task 1: Design token layer + theme CSS pass

**Files:**
- Create: `docs/design.md`
- Modify: `app/XDM/XDM.Gtk.UI/theme/xdm-dark.css`
- Modify: `app/XDM/XDM.Gtk.UI/theme/xdm-light.css`

**Interfaces:**
- Consumes: nothing (foundation task)
- Produces: token vocabulary used by every later task; CSS selectors `.sidebar-*`, `.icon-tile`, `.search-pill`, `.flat` button sizing that Tasks 3–5 attach classes to.

- [ ] **Step 1: Create `docs/design.md`** — the canonical design-language doc. Content (full file):

```markdown
# XDM Design Language

<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->

Single source of truth for XDM's UI tokens. GTK theme files inline these values
(GTK3 CSS has no variables); browser extensions mirror them as CSS custom
properties. Spec: docs/superpowers/specs/2026-08-30-xdm-design-system-modernization-design.md

## Radius
| Token | Value | Used for |
|---|---|---|
| sm | 6px | checkboxes, small chips |
| md | 8px | buttons, sidebar pills, icon tiles, linked outer corners |
| lg | 10px | menus, popovers, extension cards |
| pill | 999px | search entry, progress bars, switches, badges |

## Spacing
4 / 8 / 12 / 16 / 24 px — all margins, paddings and gaps derive from this scale.

## Color
| Role | Dark | Light |
|---|---|---|
| window | #1d1d1d | #fcfcfc |
| sidebar | #161616 | #ececec |
| view | #232323 | #ffffff |
| entry | #282828 | #ffffff |
| elevated | #262626 | #ffffff |
| button surface | #2e2e2e | #ffffff |
| border | #404040 | #cacaca |
| text | #f4f4f4 | #222222 |
| dim text | #b4b4b4 | #6b6b6b |
| accent / hover / active | #3584e4 / #5a9bea / #2767b8 | same |
| destructive / hover / active | #e01b24 / #ef4a52 / #a5121a | same |
| brand / hover | #f97316 / #fb923c | #ea580c / #f97316 |
| success | #4ade80 | #16a34a |

## Rules
1. Blue (#3584e4 family) is the ONLY interactive accent: selection, focus,
   primary buttons, progress (gradient #3584e4 → #5a9bea).
2. Orange (brand) never appears on interactive controls — logo tile, brand
   moments only.
3. Destructive stays red.
4. Every corner uses one radius from the scale; every gap from the spacing scale.
5. System font only; hierarchy via weight + dim color.

## Icons
Remix Icon outline set in app/XDM/XDM.Gtk.UI/svg-icons/ + xdm-mark.svg (brand).
```

- [ ] **Step 2: Update token header comments** in both theme files. Replace the `Tokens (...)` block (lines 5–9 dark; equivalent light) with:

```
 * Tokens (GTK3 CSS has no variables — values inlined, grouped here):
 *   window  #1d1d1d · sidebar #161616 · view #232323 · entry #282828
 *   elevated #2e2e2e · border #404040 · text #f4f4f4 · dim #b4b4b4
 *   accent #3584e4 (hover #5a9bea / active #2767b8)
 *   destructive #e01b24 (hover #ef4a52 / active #a5121a)
 *   brand #f97316 (hover #fb923c) · success #4ade80
 *   radius: sm 6 · md 8 · lg 10 · pill 999 · spacing 4/8/12/16/24
```

(Light file: keep its own surface values; append the brand/success/radius/spacing lines: `brand #ea580c (hover #f97316) · success #16a34a`.)

- [ ] **Step 3: Radius bumps** — in **both** files apply these exact changes:

| Selector | Change |
|---|---|
| `tooltip` | `border-radius: 4px` → `border-radius: 6px` |
| `popover` | add `border-radius: 10px;` (rule has none today) |
| `entry` | `border-radius: 5px` → `border-radius: 8px` |
| `button` | `border-radius: 5px` → `border-radius: 8px` |
| `checkbutton check` | `border-radius: 4px` → `border-radius: 6px` |
| `scrollbar slider` | `border-radius: 4px` → `border-radius: 999px` |
| `menu` | `border-radius: 8px` → `border-radius: 10px` |
| `.linked > button:first-child / :last-child / :only-child` | `5px` → `8px` (all six radius declarations) |

- [ ] **Step 4: Append new component rules** to **both** files (before the backdrop comment block), dark values shown; light uses its own surfaces (`row:hover` → `rgba(0,0,0,0.05)`, badge bg → `rgba(0,0,0,0.08)`, badge selected → `rgba(255,255,255,0.22)` stays, tint → `rgba(0,0,0,0.06)`):

```css
/* ===== Design-system components (docs/design.md) ===== */

/* CSD headerbar — rounded top corners matching window radius */
headerbar {
  border-radius: 10px 10px 0 0;
  padding: 4px 8px;
}

/* Sidebar list rows (ListBox; class set by MainWindow) */
list.sidebar {
  background-color: #161616;
}

list.sidebar row {
  border-radius: 8px;
  margin: 1px 4px;
  background-color: transparent;
}

list.sidebar row:hover {
  background-color: rgba(255, 255, 255, 0.06);
}

list.sidebar row:selected {
  background-color: #3584e4;
  color: #ffffff;
}

/* Sidebar section header ("Categories") via ListBox header func */
.sidebar-section-label {
  color: #8a8a8a;
  font-size: 10px;
  letter-spacing: 0.08em;
  padding: 8px 12px 4px 12px;
}

/* Count badge pill */
.sidebar-badge {
  background-color: rgba(255, 255, 255, 0.09);
  border-radius: 999px;
  padding: 1px 8px;
  color: #b4b4b4;
  font-size: 10px;
}

list.sidebar row:selected .sidebar-badge {
  background-color: rgba(255, 255, 255, 0.22);
  color: #ffffff;
}

/* Rounded icon tile (EventBox wrapper around sidebar icons) */
.icon-tile {
  border-radius: 8px;
  background-color: rgba(255, 255, 255, 0.06);
  padding: 3px;
}

/* Pill search entry (class set by MainWindow) */
entry.search-pill {
  border-radius: 999px;
  padding: 3px 12px;
}

/* Flat tool buttons — uniform hit area + md radius */
button.flat {
  min-height: 28px;
  min-width: 28px;
  border-radius: 8px;
}

/* Primary pill ("New") — filled, not flat */
button.suggested-action {
  border-radius: 8px;
  padding: 5px 14px;
}

/* Roomier list rows */
treeview row {
  padding: 4px 2px;
}

treeview row:hover {
  background-color: rgba(255, 255, 255, 0.04);
}

/* Pill progress with gradient fill (also styles CellRendererProgress) */
progressbar progress {
  background-image: linear-gradient(to right, #3584e4, #5a9bea);
}

/* Success dot next to sidebar brand mark */
.status-dot {
  color: #4ade80;
  font-size: 10px;
}
```

- [ ] **Step 5: Build + launch + verify**

Run: `~/.dotnet8/dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj`
Expected: Build succeeds (CSS is content — copy-only).

Launch: `~/.dotnet8/dotnet run --project app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj`
Expected: app starts, no CSS parse errors on stderr/stdout (`Gtk-WARNING **: ... failed to parse`), theme toggle in Settings still swaps dark/light live.

- [ ] **Step 6: Commit**

```bash
git add docs/design.md app/XDM/XDM.Gtk.UI/theme/xdm-dark.css app/XDM/XDM.Gtk.UI/theme/xdm-light.css
git commit -m "feat(ui): design token layer + radius/spacing pass"
```

---

### Task 2: Brand mark + MainWindow HeaderBar (CSD window controls)

**Files:**
- Create: `app/XDM/XDM.Gtk.UI/svg-icons/xdm-mark.svg`
- Modify: `app/XDM/XDM.Gtk.UI/MainWindow.cs` (constructor region, lines 110–163)

**Interfaces:**
- Consumes: `.status-dot` CSS class (Task 1); `GtkHelper.LoadSvg(name, size)` (existing, `Utils/GtkHelper.cs:296`).
- Produces: `private Label headerSubtitle;` on MainWindow (Task 3 updates it on category change); `private Label brandStatusDot;` (updated by `UpdateBrowserMonitorButton`).

- [ ] **Step 1: Create `svg-icons/xdm-mark.svg`** (full file):

```svg
<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->
<!-- XDM brand mark: orange gradient rounded tile + white down arrow -->
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="24" height="24">
  <defs>
    <linearGradient id="xdm-brand" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#f97316"/>
      <stop offset="1" stop-color="#fb923c"/>
    </linearGradient>
  </defs>
  <rect x="1" y="1" width="22" height="22" rx="7" fill="url(#xdm-brand)"/>
  <path d="M12 6v8.5m0 0l-3.5-3.5M12 14.5l3.5-3.5" stroke="#ffffff" stroke-width="2.2"
        stroke-linecap="round" stroke-linejoin="round" fill="none"/>
  <path d="M7 18.5h10" stroke="#ffffff" stroke-width="2.2" stroke-linecap="round"/>
</svg>
```

(The csproj already copies `svg-icons\*.*` to output — `XDM.Gtk.UI.csproj:62`.)

- [ ] **Step 2: Add HeaderBar in MainWindow constructor.** After the `windowGroup.AddWindow(this);` line (`MainWindow.cs:134`) and before `var hbMain = new HBox();` (line 136), insert:

```csharp
// CSD headerbar: brand mark + title + subtitle, themed min/max/close (spec §4.1)
var titleBox = new HBox(false, 8);
titleBox.PackStart(new Image(GtkHelper.LoadSvg("xdm-mark", 22)), false, false, 0);
titleBox.PackStart(new Label { Markup = "<b>XDM</b>" }, false, false, 0);
headerSubtitle = new Label { Text = TextResource.GetText("ALL_UNFINISHED") };
headerSubtitle.StyleContext.AddClass("dim-label");
titleBox.PackStart(headerSubtitle, false, false, 0);
brandStatusDot = new Label { Text = "●", TooltipText = TextResource.GetText("SETTINGS_MONITORING") };
brandStatusDot.StyleContext.AddClass("status-dot");
titleBox.PackStart(brandStatusDot, false, false, 4);

var headerBar = new HeaderBar
{
    CustomTitle = titleBox,
    ShowCloseButton = true,
    DecorationLayout = ":minimize,maximize,close"
};
SetTitlebar(headerBar);
```

And declare the fields with the other private fields (near `MainWindow.cs:41`):

```csharp
private Label headerSubtitle;
private Label brandStatusDot;
```

- [ ] **Step 3: Tie the brand dot to monitoring state.** In `UpdateBrowserMonitorButton()` (`MainWindow.cs:1325`), append after the existing line:

```csharp
brandStatusDot.Visible = Config.Instance.IsBrowserMonitoringEnabled;
```

- [ ] **Step 4: Build + launch + verify**

Run: `~/.dotnet8/dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj && ~/.dotnet8/dotnet run --project app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj`
Expected: window has a charcoal headerbar with orange mark, "XDM", dim "All Unfinished", green dot; minimize, maximize and close buttons all present (including on GNOME); rounded top corners; titlebar draggable; close behaves per existing hide-to-tray logic; toggling Settings → monitoring toggles the dot.

If `DecorationLayout` rejects the leading `:` on your GTK build, use `"appmenu:minimize,maximize,close"`.

- [ ] **Step 5: Commit**

```bash
git add app/XDM/XDM.Gtk.UI/svg-icons/xdm-mark.svg app/XDM/XDM.Gtk.UI/MainWindow.cs
git commit -m "feat(ui): CSD headerbar with brand mark + window controls"
```

---

### Task 3: Sidebar rebuild — ListBox, badges, Paned, RefreshCategories

**Files:**
- Modify: `app/XDM/XDM.Gtk.UI/MainWindow.cs` (replaces `CreateCategoryTree` at lines 574–645, `OnCategoryChanged` 647–690, `SwitchToInProgressView` 1185–1191, `SwitchToFinishedView` 1198–1205, `GetSelectedCategory` 1439–1447, `UpdateBrowserMonitorButton` 1325–1328, `AddToTop` ×2, `Delete` ×2, `DeleteAllFinishedDownloads`, `Set*Downloads`, constructor lines 136–147)
- Modify: `app/XDM/XDM.Gtk.UI/GtkPlatformUIService.cs` (line ~127, after SettingsDialog closes)

**Interfaces:**
- Consumes: `list.sidebar`, `.sidebar-badge`, `.sidebar-section-label`, `.icon-tile` CSS (Task 1); `headerSubtitle` (Task 2); `Helpers.IsOfCategoryOrMatchesKeyword(name, keyword, category)` (existing).
- Produces: `public void RefreshCategories()`, `public void UpdateSidebarCounts()` on MainWindow — later tasks and `GtkPlatformUIService` call these.

**Behavior parity map (must hold after rebuild):**

| Old (TreeView) | New (ListBox) |
|---|---|
| Root row 0 "All Unfinished" → in-progress view, `category = null`, Pause/Resume visible | Row key `"unfinished"` → same |
| Root row 1 "All Finished" → finished view, `category = null`, OpenFile/OpenFolder visible | Row key `"finished"` → same |
| Child rows (depth 2) → finished view + `category` set, refilter | Rows with `Category != null` → same |
| `GetSelectedCategory()`: 0 / 1 / -1 | Same return values by key (`"unfinished"`→0, `"finished"`→1, else -1) |
| `SwitchToInProgressView` / `SwitchToFinishedView` select rows 0 / 1 | Select rows by key |

- [ ] **Step 1: Replace the fields** `categoryTreeStore`, `categoryTree` (lines 22–23) with:

```csharp
// Modern sidebar: ListBox rows keyed by id (spec §4.2)
private sealed class SidebarRow
{
    public string Key = string.Empty;      // "unfinished" | "finished" | category name
    public string Label = string.Empty;
    public Category? Category;             // null for the two fixed rows
    public bool IsUnfinished;              // true only for "All Unfinished"
}

private ListBox sidebarList;
private readonly Dictionary<string, ListBoxRow> sidebarRowWidgets = new();
private readonly Dictionary<string, Label> sidebarBadges = new();
private readonly List<SidebarRow> sidebarRows = new();
private string firstCategoryKey = string.Empty; // header-func anchor
```

- [ ] **Step 2: Replace `CreateCategoryTree()` (lines 574–645) with `CreateSidebar()`** returning the ScrolledWindow; keep the `GetFontIcon` local helper verbatim:

```csharp
// Icon per category name (existing mapping, unchanged)
private static string GetFontIcon(string name)
{
    switch (name)
    {
        case "CAT_DOCUMENTS": return "file-text-line";
        case "CAT_MUSIC": return "file-music-line";
        case "CAT_VIDEOS": return "movie-line";
        case "CAT_COMPRESSED": return "file-zip-line";
        case "CAT_PROGRAMS": return "function-line";
        default: return "file-line";
    }
}

// Builds the modern sidebar: brand header + selectable rows (spec §4.2)
private Widget CreateSidebar()
{
    sidebarList = new ListBox
    {
        SelectionMode = SelectionMode.Browse
    };
    sidebarList.StyleContext.AddClass("sidebar");
    sidebarList.Selection.Changed += OnCategoryChanged;

    AddSidebarRow(new SidebarRow
    {
        Key = "unfinished",
        Label = TextResource.GetText("ALL_UNFINISHED"),
        IsUnfinished = true
    }, "arrow-down-line");
    AddSidebarRow(new SidebarRow
    {
        Key = "finished",
        Label = TextResource.GetText("ALL_FINISHED")
    }, "check-line");
    foreach (var category in Config.Instance.Categories)
    {
        AddSidebarRow(new SidebarRow
        {
            Key = category.Name,
            Label = category.DisplayName,
            Category = category
        }, GetFontIcon(category.Name));
    }

    // Section header "Categories" above the first category row (native pattern)
    sidebarList.SetHeaderFunc((row, before) =>
    {
        if (row.Name == firstCategoryKey && before != null)
        {
            row.Header = BuildSectionHeader();
        }
        else
        {
            row.Header = null;
        }
    });

    var scrolledWindow = new ScrolledWindow
    {
        OverlayScrolling = true,
        ShadowType = ShadowType.None,
        HscrollbarPolicy = PolicyType.Never,
        VscrollbarPolicy = PolicyType.Automatic
    };
    scrolledWindow.Add(sidebarList);
    scrolledWindow.SetSizeRequest(170, 200);
    scrolledWindow.ShowAll();
    return scrolledWindow;
}

// "CATEGORIES" dim uppercase label used as ListBox row header
private static Widget BuildSectionHeader()
{
    var lbl = new Label { Text = TextResource.GetText("SETTINGS_CAT").ToUpperInvariant() };
    lbl.StyleContext.AddClass("sidebar-section-label");
    lbl.Xalign = 0;
    lbl.Show();
    return lbl;
}

// One selectable sidebar row: icon tile + label + count badge
private void AddSidebarRow(SidebarRow info, string iconName)
{
    var iconTile = new EventBox { AboveChild = false, VisibleWindow = false };
    iconTile.StyleContext.AddClass("icon-tile");
    iconTile.Add(new Image(LoadSvg(iconName, 16)));

    var badge = new Label { Text = "0", Visible = false };
    badge.StyleContext.AddClass("sidebar-badge");

    var hbox = new HBox(false, 9) { MarginStart = 8, MarginEnd = 8 };
    hbox.PackStart(iconTile, false, false, 0);
    var label = new Label { Text = info.Label, Halign = Align.Start };
    hbox.PackStart(label, true, true, 0);
    hbox.PackStart(badge, false, false, 0);

    var row = new ListBoxRow { Name = info.Key };
    row.Add(hbox);

    sidebarRows.Add(info);
    sidebarRowWidgets[info.Key] = row;
    sidebarBadges[info.Key] = badge;
    if (info.Category != null && string.IsNullOrEmpty(firstCategoryKey))
    {
        firstCategoryKey = info.Key;
    }
    sidebarList.Add(row);
}

// Rebuilds category rows after Settings changes categories
public void RefreshCategories()
{
    foreach (var row in sidebarRows.Where(r => r.Category != null).ToList())
    {
        if (sidebarRowWidgets.TryGetValue(row.Key, out var widget))
        {
            sidebarList.Remove(widget);
        }
        sidebarRowWidgets.Remove(row.Key);
        sidebarBadges.Remove(row.Key);
        sidebarRows.Remove(row);
    }
    firstCategoryKey = string.Empty;
    foreach (var category in Config.Instance.Categories)
    {
        AddSidebarRow(new SidebarRow
        {
            Key = category.Name,
            Label = category.DisplayName,
            Category = category
        }, GetFontIcon(category.Name));
    }
    sidebarList.ShowAll();
    if (sidebarList.SelectedRow == null && sidebarRowWidgets.TryGetValue("unfinished", out var first))
    {
        sidebarList.SelectRow(first);
    }
    UpdateSidebarCounts();
}
```

Note: `SETTINGS_CAT=Download categories` exists in `app/XDM/Lang/English.txt` — `.ToUpperInvariant()` renders it as the uppercase section label.

- [ ] **Step 3: Replace `OnCategoryChanged` (lines 647–690):**

```csharp
// Sidebar selection drives main panel + headerbar subtitle (parity map above)
private void OnCategoryChanged(object? sender, EventArgs e)
{
    if (lvInprogress == null || lvFinished == null)
    {
        return;
    }
    var row = sidebarList.SelectedRow;
    if (row == null)
    {
        return;
    }
    var info = sidebarRows.FirstOrDefault(r => r.Key == row.Name);
    if (info == null)
    {
        return;
    }
    headerSubtitle.Text = info.Label;
    if (info.IsUnfinished)
    {
        swInProgress.ShowAll();
        swFinished.Hide();
        category = null;
        btnOpenFile.Visible = btnOpenFolder.Visible = false;
        btnPause.Visible = btnResume.Visible = true;
    }
    else
    {
        swFinished.ShowAll();
        swInProgress.Hide();
        category = info.Category;
        btnOpenFile.Visible = btnOpenFolder.Visible = true;
        btnPause.Visible = btnResume.Visible = false;
        finishedDownloadFilter.Refilter();
    }
}
```

- [ ] **Step 4: Replace the selection helpers (lines 1185–1205, 1439–1447):**

```csharp
public void SwitchToInProgressView()
{
    if (sidebarRowWidgets.TryGetValue("unfinished", out var row))
    {
        sidebarList.SelectRow(row);
    }
}

public void SwitchToFinishedView()
{
    if (sidebarRowWidgets.TryGetValue("finished", out var row))
    {
        sidebarList.SelectRow(row);
    }
}

private int GetSelectedCategory()
{
    var row = sidebarList?.SelectedRow;
    if (row == null)
    {
        return -1;
    }
    return row.Name switch
    {
        "unfinished" => 0,
        "finished" => 1,
        _ => -1
    };
}
```

Delete the old `categoryTreeStore`-based bodies entirely (and the constructor's `categoryTreeStore.GetIterFirst` default-selection block at lines 142–147, replacing with):

```csharp
// Sidebar default: "All Unfinished" — the toolbar-rich in-progress view
if (sidebarRowWidgets.TryGetValue("unfinished", out var defaultRow))
{
    sidebarList.SelectRow(defaultRow);
}
```

- [ ] **Step 5: Wrap in Paned.** In the constructor (lines 136–139) replace the HBox packing with:

```csharp
// Resizable sidebar (spec §4.2): drag the pane divider
var hpaned = new HPaned();
hpaned.Pack1(CreateSidebar(), false, false);
hpaned.Pack2(CreateMainPanel(), true, false);
Add(hpaned);
hpaned.Position = 200;
hpaned.Show();
```

- [ ] **Step 6: Add `UpdateSidebarCounts` and wire call sites.**

```csharp
// Recomputes sidebar count badges from the live stores (no timers — spec §7)
public void UpdateSidebarCounts()
{
    int inprog = 0, fin = 0;
    var perCategory = new Dictionary<string, int>();
    foreach (var cat in Config.Instance.Categories)
    {
        perCategory[cat.Name] = 0;
    }
    if (inprogressDownloadsStore != null && inprogressDownloadsStore.GetIterFirst(out var it))
    {
        do { inprog++; } while (inprogressDownloadsStore.IterNext(ref it));
    }
    if (finishedDownloadsStore != null && finishedDownloadsStore.GetIterFirst(out var ft))
    {
        do
        {
            fin++;
            var name = (string)finishedDownloadsStore.GetValue(ft, 0);
            foreach (var cat in Config.Instance.Categories)
            {
                if (Helpers.IsOfCategoryOrMatchesKeyword(name, null, cat))
                {
                    perCategory[cat.Name]++;
                }
            }
        }
        while (finishedDownloadsStore.IterNext(ref ft));
    }
    SetBadge("unfinished", inprog);
    SetBadge("finished", fin);
    foreach (var cat in Config.Instance.Categories)
    {
        SetBadge(cat.Name, perCategory[cat.Name]);
    }
}

private void SetBadge(string key, int count)
{
    if (!sidebarBadges.TryGetValue(key, out var badge))
    {
        return;
    }
    badge.Text = count.ToString();
    badge.Visible = count > 0;
}
```

Call `UpdateSidebarCounts();` at the end of: `AddToTop(InProgressDownloadItem)`, `AddToTop(FinishedDownloadItem)`, `Delete(IInProgressDownloadRow)`, `Delete(IFinishedDownloadRow)`, `DeleteAllFinishedDownloads`, `SetFinishedDownloads`, `SetInProgressDownloads`, and `RefreshCategories`.

- [ ] **Step 7: Wire settings-close refresh.** In `app/XDM/XDM.Gtk.UI/GtkPlatformUIService.cs` around line 127, after the existing `using var win = SettingsDialog.CreateFromGladeFile(...)` block completes (after `win.Destroy();` / end of using), add:

```csharp
// Categories may have changed in Settings — rebuild sidebar rows
(GetMainWindow() as MainWindow)?.RefreshCategories();
```

- [ ] **Step 8: Build + regression test + launch + verify**

Run: `~/.dotnet8/dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj && ~/.dotnet8/dotnet test app/XDM/XDM.Tests/XDM.Tests.csproj`
Expected: build passes; tests stay green.

Launch and check: rounded blue selection pill; hover pills; badges show counts (add a download); "CATEGORIES" label appears above first category; selecting All Finished/category switches lists exactly as before; search still filters finished list; headerbar subtitle follows selection; dragging the pane divider resizes the sidebar (min ~170px); Settings → add category → close → new row appears with icon and count.

- [ ] **Step 9: Commit**

```bash
git add app/XDM/XDM.Gtk.UI/MainWindow.cs app/XDM/XDM.Gtk.UI/GtkPlatformUIService.cs
git commit -m "feat(ui): ListBox sidebar with badges, resizable pane, refresh"
```

---

### Task 4: Toolbar + search pill + New button

**Files:**
- Modify: `app/XDM/XDM.Gtk.UI/MainWindow.cs` (`CreateToolbar`, lines 522–567)

**Interfaces:**
- Consumes: `.search-pill`, `button.suggested-action` padding CSS (Task 1).
- Produces: none (visual only).

- [ ] **Step 1: Style the New button as a filled pill.** In `CreateToolbar` after `btnNew = CreateButtonWithContent(...)` (line 525), add:

```csharp
// Primary action: filled blue pill instead of flat (spec §4.3)
btnNew.StyleContext.RemoveClass("flat");
btnNew.StyleContext.AddClass("suggested-action");
```

- [ ] **Step 2: Make search a pill and give the toolbar breathing room.** After the `searchEntry` creation (line 543), add:

```csharp
searchEntry.StyleContext.AddClass("search-pill");
```

And change `var toolbar = new HBox(false, 5);` (line 524) → `new HBox(false, 8)`, and `toolbar.Margin = 5;` (line 550) → `toolbar.Margin = 8; toolbar.MarginTop = 6; toolbar.MarginBottom = 6;`.

- [ ] **Step 3: Build + launch + verify**

Run: `~/.dotnet8/dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj && ~/.dotnet8/dotnet run --project app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj`
Expected: "New" renders as filled blue pill (hover lightens, not flat); search field fully rounded; icon buttons uniform squares with rounded hover; toolbar spacing visibly roomier; all buttons still trigger their menus/actions.

- [ ] **Step 4: Commit**

```bash
git add app/XDM/XDM.Gtk.UI/MainWindow.cs
git commit -m "feat(ui): toolbar pill buttons + search pill"
```

---

### Task 5: Download list refinement — two-line rows, icon tiles, pill progress

**Files:**
- Modify: `app/XDM/XDM.Gtk.UI/Utils/GtkHelper.cs` (new `LoadIconTile` near `LoadSvg`, line 296)
- Modify: `app/XDM/XDM.Gtk.UI/Utils/ThemeManager.cs` (clear tile cache on theme swap + expose IsDark)
- Modify: `app/XDM/XDM.Gtk.UI/MainWindow.cs` (`CreateInProgressListView` 692–850, `CreateFinishedListView` 852–983, `GetFileIcon` 985–991, constructor min-size line 150)

**Interfaces:**
- Consumes: `progressbar progress` gradient CSS (Task 1); `SecondaryTextAlpha` (existing).
- Produces: `GtkHelper.LoadIconTile(string name, int dimension, bool dark)` — cached rounded-tile pixbufs; `ThemeManager.IsDark` static bool.

- [ ] **Step 1: Expose theme mode + cache clear.** In `ThemeManager.cs` add a field and set it in `ApplyTheme(bool dark)` (top of method): `IsDark = dark;` plus:

```csharp
// Current theme mode for icon-tile tinting
public static bool IsDark { get; private set; } = true;

// Called at the end of ApplyTheme's successful CSS-swap branch:
GtkHelper.ClearIconTileCache();
```

- [ ] **Step 2: Add tile renderer + cache to `GtkHelper.cs`** (after `LoadSvg`):

```csharp
// Rounded icon tiles: SVG icon composited on a tinted rounded square (spec §4.4)
private static readonly Dictionary<string, Gdk.Pixbuf> iconTileCache = new();

public static void ClearIconTileCache() => iconTileCache.Clear();

public static Gdk.Pixbuf LoadIconTile(string name, int dimension = 28, bool? dark = null)
{
    var isDark = dark ?? ThemeManager.IsDark;
    var key = $"{name}@{dimension}|{(isDark ? "d" : "l")}";
    if (iconTileCache.TryGetValue(key, out var cached))
    {
        return cached;
    }
    var icon = LoadSvg(name, dimension - 10);
    using var surface = new Cairo.ImageSurface(Cairo.Format.Argb32, dimension, dimension);
    using (var cr = new Cairo.Context(surface))
    {
        double r = 8, d = dimension;
        cr.NewSubPath();
        cr.Arc(d - r, r, r, -Math.PI / 2, 0);
        cr.Arc(d - r, d - r, r, 0, Math.PI / 2);
        cr.Arc(r, d - r, r, Math.PI / 2, Math.PI);
        cr.Arc(r, r, r, Math.PI, 1.5 * Math.PI);
        cr.ClosePath();
        // Neutral tint token: white @6% on dark, black @6% on light
        if (isDark)
        {
            cr.SetSourceRGBA(1.0, 1.0, 1.0, 0.06);
        }
        else
        {
            cr.SetSourceRGBA(0.0, 0.0, 0.0, 0.06);
        }
        cr.Fill();
    }
    surface.Flush();
    var tile = Gdk.Pixbuf.FromSurface(surface, 0, 0, dimension, dimension);
    icon.Composite(tile, 5, 5, icon.Width, icon.Height, 5, 5, 1, 1, Gdk.InterpType.Bilinear, 255);
    iconTileCache[key] = tile;
    return tile;
}
```

Binding fallback (only if `Gdk.Pixbuf.FromSurface(...)` does not exist in GtkSharp 3.24.24.38): replace that single line with `surface.WriteToPng(ms); ms.Position = 0; var tile = new Gdk.Pixbuf(ms);` over a `using var ms = new MemoryStream();` — both APIs exist. If neither compiles, return plain `LoadSvg(name, dimension - 6)` with a code comment and note it in the task report (pre-approved degradation).

- [ ] **Step 3: Use tiles in `GetFileIcon`** (MainWindow.cs:985–991):

```csharp
void GetFileIcon(ICellLayout cell_layout, CellRenderer cell, ITreeModel tree_model, TreeIter iter)
{
    var name = (string)tree_model.GetValue(iter, 0);
    ((CellRendererPixbuf)cell).Pixbuf = GtkHelper.LoadIconTile(
        IconResource.GetSVGNameForFileType(name), 28);
}
```

- [ ] **Step 4: Two-line name cell in the in-progress list.** In `CreateInProgressListView`, replace the plain name renderer block (lines 760–763) with a markup data func (name line 1, dim status line 2):

```csharp
var fileNameRendererText = new CellRendererText();
fileNameColumn.PackStart(fileNameRendererText, false);
fileNameColumn.SetCellDataFunc(fileNameRendererText, new CellLayoutDataFunc(
    (_, cell, model, iter) =>
    {
        var name = (string)model.GetValue(iter, 0);
        var status = (string)model.GetValue(iter, 4);
        ((CellRendererText)cell).Markup =
            $"{GLib.Markup.EscapeText(name)}\n" +
            $"<span alpha=\"{SecondaryTextAlpha}\" size=\"8500\">{GLib.Markup.EscapeText(status)}</span>";
    }));
```

- [ ] **Step 5: Responsive columns.** In `CreateInProgressListView`: on `fileNameColumn` set `Expand = true;` and widen base `FixedWidth = 240`. On `progressColumn` set `MinWidth = 90; FixedWidth = 110;`. In `CreateFinishedListView`: `fileNameColumn.Expand = true;`. In the constructor, after `SetDefaultSize(800, 500)` (line 150), add:

```csharp
// Responsive floor: nothing clips below this (spec §4.4)
SetSizeRequest(640, 420);
```

- [ ] **Step 6: Build + launch + verify**

Run: `~/.dotnet8/dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj && ~/.dotnet8/dotnet run --project app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj`
Expected: in-progress rows show rounded icon tiles, filename over dim status line, pill progress bars with gradient; resizing the window grows/shrinks the name column (others fixed); below 800px nothing clips until 640×420; finished list unchanged layout but tiles + expand; theme toggle re-tints tiles (cache cleared).

- [ ] **Step 7: Commit**

```bash
git add app/XDM/XDM.Gtk.UI/Utils/GtkHelper.cs app/XDM/XDM.Gtk.UI/Utils/ThemeManager.cs app/XDM/XDM.Gtk.UI/MainWindow.cs
git commit -m "feat(ui): two-line list rows, icon tiles, pill progress"
```

---

### Task 6: Dialogs — headerbars + spacing pass

**Files:**
- Modify: `app/XDM/XDM.Gtk.UI/Utils/GtkHelper.cs` (new `AttachHeaderBar`)
- Modify (headerbar call): `Dialogs/Settings/SettingsDialog.cs`, `Dialogs/NewDownload/NewDownloadWindow.cs`, `Dialogs/VideoDownloader/VideoDownloaderWindow.cs`, `Dialogs/BatchDownload/BatchDownloadDialog.cs`, `Dialogs/AdvancedDownload/AdvancedDownloadWindow.cs`, `Dialogs/PropertiesDialog/PropertiesDialog.cs`, `Dialogs/QueueManager/QueueManagerDialog.cs`, `Dialogs/DeleteConfirm/DeleteConfirmDialog.cs`, `Dialogs/About/AboutDialog.cs`, `Dialogs/Language/LanguageDialog.cs` (locate exact folders by filename glob if paths differ)
- Modify (spacing pass): `glade/settings-dialog2.glade`, `glade/new-download-window.glade`, `glade/queue-manager-dialog.glade`

**Interfaces:**
- Consumes: headerbar CSS (existing + Task 1 radius).
- Produces: `GtkHelper.AttachHeaderBar(Dialog dlg, string title)`.

- [ ] **Step 1: Add the helper to `GtkHelper.cs`:**

```csharp
// Compact close-only headerbar for glade dialogs (spec §5)
public static void AttachHeaderBar(Dialog dlg, string title)
{
    var hb = new HeaderBar
    {
        Title = title,
        ShowCloseButton = true
    };
    dlg.SetTitlebar(hb);
}
```

- [ ] **Step 2: Wire into the 10 dialogs.** In each dialog's `CreateFromGladeFile(Window parent, WindowGroup group)` static (pattern: `return new X(builder, parent, group);`), change to:

```csharp
var dlg = new X(builder, parent, group);
GtkHelper.AttachHeaderBar(dlg, TextResource.GetText("TITLE_SETTINGS")); // per-dialog key
return dlg;
```

Use each dialog's existing title key: Settings → `TITLE_SETTINGS`; About → `MENU_ABOUT`; Language → `MENU_LANG`; DeleteConfirm → `DESC_DEL`; QueueManager → `DESC_Q_TITLE`; the rest reuse their existing `Title =` key already set in their `.cs` — grep each file for `TextResource.GetText` near its title assignment and use that key.

- [ ] **Step 3: Spacing pass on the 3 glade files.** In each, find the top-level container(s): the `<object class="GtkBox">` that is the dialog's direct child (id typically `dialog-vbox*` / `mainbox`) and any `GtkButtonBox` (`dialog-action_area`). Change/add their spacing properties to the scale values:

```xml
<property name="border_width">12</property>   <!-- was 5 or missing -->
<property name="spacing">12</property>        <!-- was 2–6 -->
```

Apply the same pattern to second-level `GtkBox` containers' `spacing` (bump by one scale step: 2→4, 4→8, 6→12). Do not touch widget IDs, signals, or packing.

- [ ] **Step 4: Build + launch + verify**

Run: `~/.dotnet8/dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj && ~/.dotnet8/dotnet run --project app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj`
Expected: each of the 10 dialogs opens (☰ menu → Settings / Language / About; New ▾ → all three; right-click download → Properties / Delete; ☰ → Queue scheduler) with a compact charcoal headerbar + close button only, rounded top corners; content margins visibly roomier; every control still works (settings tabs, category add/edit, queue start/stop).

- [ ] **Step 5: Commit**

```bash
git add app/XDM/XDM.Gtk.UI/Utils/GtkHelper.cs app/XDM/XDM.Gtk.UI/Dialogs app/XDM/XDM.Gtk.UI/glade/settings-dialog2.glade app/XDM/XDM.Gtk.UI/glade/new-download-window.glade app/XDM/XDM.Gtk.UI/glade/queue-manager-dialog.glade
git commit -m "feat(ui): dialog headerbars + spacing scale pass"
```

---

### Task 7: Docs pointer + final verification sweep

**Files:**
- Modify: `AGENTS.md` (Conventions section)

- [ ] **Step 1: Add pointer.** Under `## Conventions` in `AGENTS.md`:

```markdown
- Design system (tokens, radii, spacing, colors): [docs/design.md](docs/design.md) — all UI changes follow it.
```

- [ ] **Step 2: Full sweep.**

Run: `~/.dotnet8/dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj && ~/.dotnet8/dotnet test app/XDM/XDM.Tests/XDM.Tests.csproj`
Expected: build clean, tests green.

Launch and walk the checklist: dark + light toggle (no restart); sidebar select/hover pills + badges + resize; toolbar pills; two-line rows + tiles + pill progress; window controls (min/max/close); 10 dialogs' headerbars; tray behavior unchanged; search; category filter; queue scheduler; delete flows. Compare against mockups: `.superpowers/brainstorm/459457-1788096131/content/sidebar-list-style.html` (option B) and `headerbar-variants.html` (option 1) — open directly in a browser.

- [ ] **Step 3: Commit**

```bash
git add AGENTS.md
git commit -m "docs: point AGENTS.md at design system"
```
