# Changelog

All notable user-facing changes to FetchFlow are documented here.
For end users: what looks different, what's smoother, what no longer crashes.

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
