<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->
# Chrome Web Store Listing — FetchFlow Browser Helper

> Last Updated: 2026-09-01

## Store Listing

**Extension Name** [REQUIRED]
FetchFlow Browser Helper

**Short Description** [REQUIRED]
Integrates your browser with the FetchFlow Download Manager desktop application for high-speed downloads.

**Detailed Description** [REQUIRED]
FetchFlow Browser Helper connects your browser directly to the FetchFlow Download Manager desktop app (Linux and Windows), enabling seamless download handoff, multi-connection acceleration, and download resumption.

Key Features:
- Seamless Download Takeover: Automatically routes browser file downloads to FetchFlow for faster multi-segmented downloads.
- Pause and Resume Support: Resumes broken downloads without restarting from scratch.
- Chunked Large File & Blob Handling: Captures streaming blob downloads and delivers them directly to your local disk.
- Context Menu Integration: Right-click any link or media item to download directly with FetchFlow.
- One-Click Toggle: Easily enable or disable download monitoring from the extension popup.

How to Use:
1. Ensure the FetchFlow desktop application is installed and running on your system.
2. Click the FetchFlow icon in your browser toolbar to verify the connection status.
3. Start downloading any file in your browser — FetchFlow will automatically handle the download.

Policy Note:
FetchFlow Browser Helper strictly adheres to Chrome Web Store Developer Program Policies. This extension does NOT download or extract content from YouTube.

Privacy & Data Use:
FetchFlow Browser Helper operates 100% locally. No browsing data, download URLs, cookies, or personal information is transmitted to external servers or third parties. Data is passed exclusively over local loopback (127.0.0.1) to your local FetchFlow desktop client.

Support & Source Code:
FetchFlow is open-source software under the GNU GPL-3.0 license.
Website: https://mayanktaker.com
Source code & Issues: https://github.com/mayanktaker/fetchflow

**Category** [REQUIRED]
Productivity

**Single Purpose** [REQUIRED]
Routes file downloads and user-initiated media requests from the browser to the local FetchFlow Download Manager desktop client.

**Primary Language** [REQUIRED]
English

## Graphics & Assets

| Asset | Dimensions | Status | Filename |
|---|---|---|---|
| Store Icon [REQUIRED] | 128×128 PNG | ✅ Ready | `app/XDM/chrome-extension/icon128.png` |
| Screenshot 1 [REQUIRED] | 1280×800 | 🟡 Needs export | `docs/link1.png` (or desktop screenshot) |
| Screenshot 2 [RECOMMENDED] | 1280×800 | 🟡 Needs export | `docs/link2.png` |
| Small Promo Tile [RECOMMENDED] | 440×280 | ⬜ Optional | |
| Marquee Promo Tile | 1400×560 | ⬜ Optional | |

## Permissions Justification

| Permission | Type | Justification |
|---|---|---|
| `downloads` | permissions | Required to detect when a file download is initiated in the browser and cancel the default single-threaded browser download so FetchFlow desktop can accelerate it. |
| `webRequest` | permissions | Required to inspect HTTP response headers (Content-Type and Content-Disposition) to determine if a requested URL is a downloadable file matching the user's download rules. |
| `webNavigation` | permissions | Required to observe page navigation changes to maintain active download referrer chains when downloads are initiated. |
| `tabs` | permissions | Required to obtain the originating page URL and page title to name downloaded files accurately and provide correct HTTP Referer headers. |
| `storage` | permissions | Required to store user extension preferences (such as monitoring toggle state and blocked host exceptions) locally across browser restarts. |
| `contextMenus` | permissions | Required to add 'Download with FetchFlow' options to browser right-click menus for links, images, and audio/video elements. |
| `alarms` | permissions | Required for periodic lightweight heartbeat reconnection checks to the local desktop client. |
| `cookies` | permissions | Required to retrieve session cookies for the specific download URL so the desktop downloader can authenticate and download files from password-protected or session-gated websites. Cookies are never sent to external servers. |
| `*://*/*` | host_permissions | Required so the extension can intercept downloads and inspect file headers across any user-visited website where a download is initiated. |

## Privacy & Data Use

### Data Collection

**Does the extension collect user data?** No

| Data Type | Collected? | Transmitted Off-Device? | Purpose | Shared with Third Parties? |
|---|---|---|---|---|
| Personally identifiable info | No | No | N/A | No |
| Health info | No | No | N/A | No |
| Financial info | No | No | N/A | No |
| Authentication info | Optional (session cookies) | No (passed only to local desktop client on `127.0.0.1`) | User-authorized download authentication | No |
| Personal communications | No | No | N/A | No |
| Location | No | No | N/A | No |
| Web history | No | No | N/A | No |
| User activity | No | No | N/A | No |
| Website content | No | No | N/A | No |

### Data Use Certification
- [x] Data is NOT sold to third parties
- [x] Data is NOT used for purposes unrelated to the extension's core functionality
- [x] Data is NOT used for creditworthiness or lending purposes

## Privacy Policy

**Privacy Policy URL** [REQUIRED]
https://mayanktaker.com/privacy (or hosted at `docs/privacy.html` via GitHub Pages)

## Distribution

**Visibility**: Public
**Regions**: All regions
**Pricing**: Free

## Developer Info

**Publisher Name** [REQUIRED]
Mayanktaker Computers & Web Development

**Contact Email** [REQUIRED]
mayanktaker@mayanktaker.com

**Homepage URL** [RECOMMENDED]
https://mayanktaker.com

## Version History

| Version | Date | Changes | Status |
|---|---|---|---|
| 9.1.4 | 2026-09-01 | MV3 compliance, chunked blob support, 2026 data privacy updates | Ready |

## Review Notes

### Localhost IPC Communication
This extension acts as a bridge for the native FetchFlow Download Manager application on Linux and Windows. It communicates with the local application via loopback WebSocket/HTTP on `127.0.0.1:8597` (or fallback ports `8598-8603`). No remote server communication occurs.
