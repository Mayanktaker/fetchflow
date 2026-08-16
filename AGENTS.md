# XDM Release Instructions

This document describes how to generate a complete release for Xtreme Download Manager.

## When to run

Run the release process when the user asks to **"generate release"** or **"build release"**.

## Single command

```bash
bash build_all.sh
```

This builds every artifact and places them in `xdm-release/`.

## What gets built

| Artifact | Format | Target |
|----------|--------|--------|
| Chrome extension | `.zip` | Chrome Web Store |
| Firefox extension | `.xpi` | Firefox Add-ons (AMO) |
| Global Linux | `.tar.gz` | Any modern x64 Linux distro |
| RPM | `.rpm` | Fedora / RHEL / openSUSE |
| DEB | `.deb` | Debian / Ubuntu / derivatives |
| Arch Linux | `.pkg.tar.zst` | Arch / Manjaro / EndeavourOS |

## Version source of truth

The version is defined in:

```
app/XDM/XDM.Linux.Installer/version.env
```

All packaging scripts source this file, so bump the version there before releasing.

## Version bumping

When the user asks to **"generate new release"** or **"build new release"**, bump the point release version in `version.env` before building. For example:
- `9.1.1` → `9.1.2`

After bumping, run `bash build_all.sh` as usual.

## Output

All release artifacts land in the project-root `xdm-release/` folder.

## Notes

- The `.NET` app is published as a self-contained single file (`linux-x64`) before packaging.
- Browser extensions are packaged from their respective source folders.
- `build_all.sh` handles directory cleanup, build ordering, and artifact collection.
