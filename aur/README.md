<!-- © 2026 Mayanktaker Computers & Web Development | https://mayanktaker.com -->

# FetchFlow on AUR (`fetchflow-bin`)

Binary repackage of the official Arch build for CachyOS / Manjaro / EndeavourOS users
who prefer `yay` / `paru` over the universal installer.

```bash
yay -S fetchflow-bin
# or
paru -S fetchflow-bin
```

## First-time submit (Mayank, AUR account required)

```bash
git clone ssh://aur@aur.archlinux.org/fetchflow-bin.git /tmp/opencode/aur-push
cp aur/fetchflow-bin/PKGBUILD aur/fetchflow-bin/.SRCINFO /tmp/opencode/aur-push/
cd /tmp/opencode/aur-push
git add PKGBUILD .SRCINFO
git commit -m "Initial import fetchflow-bin <version>"
git push origin master
```

## Per-release bump

1. Update `pkgver` in `aur/fetchflow-bin/PKGBUILD` (match `version.env`).
2. Refresh the checksum after the GitHub release publishes the `.pkg.tar.zst`:
   ```bash
   cd aur/fetchflow-bin
   updpkgsums
   makepkg --printsrcinfo > .SRCINFO
   ```
3. Commit + push to the AUR repo as above.

## Notes

- `provides/conflicts: fetchflow` keeps it interchangeable with a source-built package.
- `license: GPL2` matches the repo `LICENSE` (GPL-2.0).
- Every GitHub release carries a copy under `fetchflow-release/aur/` (staged by
  `build_all.sh` / `release.yml`) — the AUR repo remains the canonical submit target.
- Users without an AUR helper can use `scripts/install-fetchflow.sh` instead.
