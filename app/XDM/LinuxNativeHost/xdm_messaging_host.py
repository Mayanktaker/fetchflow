#!/usr/bin/env python3
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
#
# DEPRECATED — intentionally NOT a functional native messaging host.
#
# XDM's Linux browser integration does NOT use native messaging. It uses:
#   (1) the OS-registered xdm-app:// URL scheme (registered via the .desktop file), and
#   (2) a loopback HTTP relay served by the GTK app on 127.0.0.1 (port 8597 + fallback).
#
# This file previously shipped as a 0-byte stub. It now exists only to document the
# deprecation so packagers and users don't expect a Python host to be wired up.
#
# To implement a real Python native-messaging host in the future, follow the spec at
# https://developer.mozilla.org/docs/Mozilla/Add-ons/WebExtensions/Native_messaging
# and install a manifest JSON under ~/.mozilla/native-messaging-hosts/ (Firefox) or
# ~/.config/<browser>/NativeMessagingHosts/ (Chromium family).
import sys

def main():
    # Read the 4-byte message-length prefix so stdin stays drained if a browser ever spawns us.
    try:
        length_bytes = sys.stdin.buffer.read(4)
        if length_bytes:
            # No-op: we intentionally do not process native messages on Linux.
            pass
    except Exception:
        pass

if __name__ == "__main__":
    main()
