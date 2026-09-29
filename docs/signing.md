<!-- © 2026 Mayanktaker Computers & Web Development | https://mayanktaker.com -->

# Package signing (GPG)

Releases ship **unsigned** until a signing key is configured. Once configured, CI attaches
binary detached `.sig` files (what `pacman -U <url>` demands) to every Linux native
package plus `fetchflow-signing-key.asc` (the public key) to each release.

## One-time setup (Mayank)

```bash
# 1. Create a dedicated signing key (no passphrase = leave FETCHFLOW_GPG_PASSPHRASE unset)
gpg --quick-generate-key "FetchFlow <you@example.com>" rsa4096 sign 5y

# 2. Store the private key as a repo secret (public key is auto-published per release)
gpg --armor --export-secret-keys "FetchFlow" | gh secret set FETCHFLOW_GPG_PRIVATE_KEY --repo Mayanktaker/fetchflow
# Optional: gh secret set FETCHFLOW_GPG_PASSPHRASE --repo Mayanktaker/fetchflow

# 3. Note the fingerprint for the verify steps below
gpg --list-keys --fingerprint "FetchFlow"
```

Next `v*` tag build signs automatically; without the secret the step logs
"No signing key configured" and ships unsigned.

## Verifying a download (users)

```bash
curl -fsSL https://github.com/Mayanktaker/fetchflow/releases/latest/download/fetchflow-signing-key.asc -o /tmp/ffkey.asc
gpg --show-keys /tmp/ffkey.asc   # confirm the fingerprint matches the published one
```

## Direct-URL install on Arch (needs the key in pacman-keyring)

```bash
sudo pacman-key --add /tmp/ffkey.asc
sudo pacman-key --lsign-key <FINGERPRINT>
sudo pacman -U https://github.com/Mayanktaker/fetchflow/releases/latest/download/fetchflow-9.1.15.18-1-x86_64.pkg.tar.zst
```

Without this, use the download-first form (no signature required for local files).
