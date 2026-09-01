---
name: Bug report
about: Create a report to help us improve

---

### FetchFlow is based on XDM by subhra74 — see https://github.com/subhra74/xdm for the original project ###

**PLEASE DO NOT JUST SAY "It does not work, or something not working etc." Provide enough relevent details so that the issue can be analyzed and reproduced easily**

**Describe the bug**
A clear and concise description of what the bug is.

**To Reproduce**
Steps to reproduce the behavior:
1. Go to '...'
2. Click on '....'
3. Scroll down to '....'
4. See error

**Expected behavior**
A clear and concise description of what you expected to happen.

**Screenshots**
If applicable, add screenshots to help explain your problem.

**Environment — please complete:**
 - OS / Distro: [e.g. Fedora 42, Ubuntu 24.04, Arch]
 - Session type: [Wayland / X11 — run `echo $XDG_SESSION_TYPE`]
 - Desktop: [e.g. KDE Plasma 6, GNOME 47, Sway/Hyprland/COSMIC]
 - FetchFlow version: [e.g. 9.1.4 — see `app/XDM/XDM.Linux.Installer/version.env` or Help → About]
 - Browser + extension version: [e.g. Firefox 128 / Chrome 128, FetchFlow extension 9.1.4]
 - Install type: [rpm / deb / Arch / tarball / Flatpak]
 - OS: [e.g. Linux/Windows]
 - Browser [e.g. chrome, Firefox]
 - FetchFlow extension version [e.g. 9.1.4]

**Logs — REQUIRED: crash.log (always-on, 5 MB cap with rotation)**
> FetchFlow writes every unhandled exception to `crash.log` (capped at 5 MB with automatic rotation) via `AppDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException` + `GLib.ExceptionManager.UnhandledException`. **Please attach this file to every bug report — even if the terminal was detached, "crashed after some time" is diagnosable from it.**

- Standard install (native package / portable): `~/.fetchflow-app-data/crash.log` — legacy installs may still use `~/.xdm-app-data/crash.log`, check both.
- Flatpak / sandbox (when `XDG_CONFIG_HOME` is set): `$XDG_CONFIG_HOME/fetchflow/crash.log` (and `$XDG_DATA_HOME` equivalent).

Collect and attach:

```bash
# Preferred location (current installs)
cat ~/.fetchflow-app-data/crash.log
# Legacy fallback (auto-migrated installs)
cat ~/.xdm-app-data/crash.log
# Flatpak / sandbox
cat "$XDG_CONFIG_HOME/fetchflow/crash.log"
# If the file is large (5 MB cap), last 100 lines is enough for triage:
tail -100 ~/.fetchflow-app-data/crash.log
tail -100 ~/.xdm-app-data/crash.log
tail -100 "$XDG_CONFIG_HOME/fetchflow/crash.log"
```

Drag-and-drop `crash.log` into the issue, or paste the `tail -100` output inside a fenced code block. If there is no `crash.log` yet, state that and include the steps + timestamp of the crash.

<details><summary>Optional debug log (only if <code>FETCHFLOW_DEBUG_MODE=1</code> was enabled — legacy <code>XDM_DEBUG_MODE</code> still works)</summary>

If you reproduced the issue with debug logging on, also attach `log.txt`:

```bash
cat ~/.fetchflow-app-data/log.txt
cat ~/.xdm-app-data/log.txt
cat "$XDG_CONFIG_HOME/fetchflow/log.txt"
```

Enable it only when asked or when you need verbose tracing:

```bash
FETCHFLOW_DEBUG_MODE=1 fetchflow   # or FETCHFLOW_DEBUG_MODE=1 /opt/fetchflow/fetchflow (legacy: XDM_DEBUG_MODE still works)
```

See also: https://github.com/Mayanktaker/fetchflow/wiki/Generate-log-for-troubleshooting

</details>

**Additional context**
Add any other context about the problem here.
