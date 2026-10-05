# Changelog

All notable user-facing changes to FetchFlow are documented here.
For end users: what looks different, what's smoother, what no longer crashes.

---
## v9.1.16.0 (2026-10-05)

### What's new

- **FetchFlow now saves `.torrent` files:** if you click a torrent link, FetchFlow picks it up like any other download and files it under Documents with the rest of your documents. It saves the torrent file itself — opening it is up to your torrent client
- **Extension pages now name themselves:** the connection-error, monitoring-disabled and register pages used to show a bare extension address in the tab. They now say what they are, so you can tell at a glance which one you are looking at

### Bug fixes

- **Fewer silent failures while finishing a download:** if something went wrong while closing a connection or saving a file, FetchFlow now writes it to its log instead of quietly forgetting it. If a download ever appeared to stall for no reason, this is the place to look
- **Safer handling of settings that arrive from the browser:** the value a website can suggest for a file name is cleaned more consistently before it reaches your disk
- **Settings and downloads start up more reliably under heavy threading:** two internal startup paths could in rare cases hand back a half-initialised object to a download running in the background

---
## v9.1.15.18 (2026-09-29)

### What's new

- **Your theme and color scheme now remember themselves:** both used to reset to the default every time you opened FetchFlow
- **The color scheme list no longer opens empty:** it shows the scheme that is actually in use
- **New defaults:** Charcoal Blue for dark mode and Classic Blue for light mode, so following your system picks a classic blue either way
- **Drag any column to make it wider:** the downloads header is now a real column header — pull the edge between selection, file name and size to fit your screen, and the headings move with the columns
- **A header you can actually read:** the strip above the list is taller and roomier, the sort arrow sits on the column you're sorting by, and the other columns keep a small hint that they can be sorted too
- **Select-all in the header:** the tick box in the header now shows whether nothing, some or everything is selected, and clicking it fills or clears the list in one go
- **More air between downloads:** list items are spaced further apart so each download is easier to scan and click
- **Softer, more modern surfaces on Linux:** buttons, menus, popovers, search boxes and cards drop the old hard-edge gradients in favour of flat surfaces with rounder corners, matching the look of current GNOME and other modern Linux desktops
- **Roomier rows on Windows too:** the Windows download list now uses the same roomier spacing and a clearly visible column divider you can drag
- **Buttons that all match:** the Add / Edit / Delete / Defaults row in Settings and the queue buttons no longer look like plain text next to Cancel and Save — every button in a row now has the same border and lights up the same way on hover
- **Hover you can actually see:** borderless buttons (the small Copy, Default and Browse links) now show a clearly visible highlight when you point at them, instead of barely reacting

### Bug fixes

- Fixed the download list header on Linux looking clipped and misaligned against the file names and sizes
- **The column header bar is visible again on light themes** — it had gone transparent and blended into the background
- **Columns can be resized again** on every theme, light and dark
- **You can now see where to drag a column:** a divider sits between the download list headings and lights up when you point at it
- **Selecting several downloads works again:** the tick box now responds on click, and it is a larger, clearer box so it is easy to hit and easy to see. Row hover highlighting is fixed too
- **The header's select-all tick is now big enough to read and hit**
- **Softer unfocused rows:** when FetchFlow loses focus, selected downloads no longer turn the same colour as the header — they fade to a light tint instead
- **Correct icon in the GNOME dock and taskbar:** the window now carries the same app ID as its launcher entry, so the shell shows the FetchFlow icon instead of a generic one

---
## v9.1.15.17 (2026-09-28)

### What's new

- **Cleaner upgrade on Windows:** if an older version left a startup shortcut behind, FetchFlow now clears it on first launch so the app no longer starts twice at login

### Bug fixes

- Fixed the download page on the website occasionally lagging one release behind

---

## v9.1.15.16 (2026-09-28)

### What's new

- **Correct icon in the Linux system tray:** FetchFlow now shows its own orange download icon on GNOME, KDE, and Wayland panels instead of a generic three-dot placeholder
- **Launch at login now actually works:** FetchFlow reliably starts in the background when you sign in, and repoints itself to the current install location after an update or reinstall
- **Consistent start-at-login on Windows:** the installer and the in-app setting now use the same mechanism, so choosing it during setup and toggling it in Settings always agree

### Bug fixes

- Fixed the "Start FetchFlow when I sign in" option not switching launch-at-login off when unticked
- Fixed the system tray showing three dots instead of the FetchFlow icon
- Fixed launch-at-login silently failing after a reinstall while Settings still showed it as switched on
- Fixed FetchFlow starting twice at login on Windows when the installer option and the in-app setting were both used
- Fixed Settings showing start-at-login as off on Windows even though an earlier install had turned it on


## v9.1.15.15 (2026-09-27)

### What's new

- **More robust one-line Linux installer:** piping the installer via `curl | bash` now works reliably on all distros

### Bug fixes

- Fixed the app failing to start on Arch, CachyOS, and Manjaro with an application-bundle error (packaging no longer strips the binary)

---

## v9.1.15.14 (2026-09-27)

### What's new

- **One-line Linux installer:** a single command now detects your distro (Arch, CachyOS, Manjaro, Fedora, Debian, openSUSE) and installs the correct native package automatically
- **Arch, CachyOS & Manjaro support:** brand-new native system package plus AUR availability (`yay -S fetchflow-bin`) — no more manual tarball setup

### Bug fixes

- Fixed installation failing on Arch-based distros with a "conflicting files" error
- Fixed missing license info in the Debian package

---

## v9.1.15.13 (2026-09-17)

### What's new

- **Refined Windows Desktop Experience:**
  - Modern integrated title bar featuring an instant Sun/Moon theme toggle button
  - Dynamic category subtitle in the window title showing your active view (All Unfinished, Videos, Music, etc.)
  - Card-style download list with elegant rounded corners and smooth animated hover and selection transitions
  - Modern capsule pill search bar for intuitive searching and filtering
  - Rounded action buttons, toolbar cards, and status bar matching the Linux desktop interface
  - Softened, comfortable dark mode colors with improved contrast and eye comfort
- **Windows Package Manager (Winget) Support:**
  - Official Winget installation manifests for seamless one-line setup and package management
- **Visual Parity:**
  - Complete aesthetic and fluidity alignment between Linux and Windows editions

### Bug fixes

- Fixed window border clipping when maximizing on Windows
- Fixed download row selection highlight borders to smoothly follow the active color theme
- Fixed window close button behavior to consistently minimize to tray

---

## v9.1.15.12 (2026-09-17)

### What's new

- **Windows UI enhancements:**
  - Modern About dialog with high-resolution FetchFlow branding and quick links
  - "Verify Checksum" button directly inside the download completion window
  - 10-second auto-close countdown timer on completion window that pauses when hovering
  - Dynamic file-type icons in the download complete dialog
  - Streamlined main menu with single Import / Export access matching Linux
- Automated user-facing release notes for all published releases

### Bug fixes

- Fixed missing "Schedule" option in the in-progress downloads menu on Windows
- Fixed "New Queue" label in the video download window to correctly show "File:"
- Fixed unlocalized title in the download progress window on Windows
- Fixed settings window navigation when opened from specific menu shortcuts

---

## v9.1.15.11 (2026-09-16)

### What's new

- **Windows (WPF) reaches full parity with Linux (GTK)**
  - 14 color schemes (7 dark + 7 light) with live switching and Follow System
  - Checksum and Import/Export dialogs now on Windows
  - GTK-parity menu icons and Remix geometry icon library
  - Card-row download list styling matching Linux
- Official FetchFlow branding across exe, installer, tray icon, and shortcuts
- Windows setup installer and portable ZIP now built automatically by CI
- First-ever Windows artifacts included in GitHub releases

### Bug fixes

- Fixed invisible checkboxes and missing progress digits in download lists
- Fixed browser extension blob hijack issue
- Fixed Application alias for markup compile on Windows

---

## v9.1.15.4 (2026-09-14)

### What's new

- MP3 conversion with bitrate selector for audio/video downloads
- Audio bitrate badges in the video download window
- Tab refresh button in the browser extension
- Firefox AMO store link added to the app and website

### Bug fixes

- Filtered MHTML storyboard sheets from media detection
- Fixed YouTube audio extraction pipeline

---

## v9.1.15.3 (2026-09-07)

### What's new

- Multi-signal tab matching for YouTube and consolidated resolution detection
- Firefox AMO store link in README and website

---

## v9.1.15.2 (2026-09-05)

### Bug fixes

- Extension stability and media detection improvements

---

*Older releases: see [GitHub Releases](https://github.com/Mayanktaker/fetchflow/releases) for the full history.*
