<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->
# Firefox Add-on (AMO) Listing — FetchFlow Browser Helper

> Last Updated: 2026-11-26
> Companion document: [`CHROMEWEBSTORE.md`](CHROMEWEBSTORE.md) (Chrome Web Store submission).
> Both listings describe the same feature set; only the permission names and the
> platform-specific plumbing differ.

## Listing Metadata

**Add-on ID** [REQUIRED]
`fetchflow-browser-helper@mayanktaker.com`

**Name** [REQUIRED]
FetchFlow Browser Helper

**Summary** [REQUIRED — max 250 characters]
Sends browser downloads and video streams to the FetchFlow Download Manager on your own machine — multi-connection acceleration, resumable transfers, and blob capture, all over localhost.

**Description** [REQUIRED]
FetchFlow Browser Helper connects Firefox to the FetchFlow Download Manager desktop app (Linux and Windows), so a download you start in the browser finishes faster in FetchFlow.

Key features:
- **Download takeover:** a file download is handed to FetchFlow, which fetches it over multiple connections and can resume it later.
- **Attachment fast-path:** a response that says "save this file" is taken over before Firefox writes anything, so the browser's save dialog never interrupts you.
- **Blob and streaming capture:** in-page blob media and segmented streams are captured and delivered to your local disk.
- **Right-click integration:** "Download with FetchFlow" for links, images, audio and video.
- **Popup inspector:** see the streams captured on the current tab, pick a quality, and toggle monitoring.
- **One-click connection test:** the popup's Test Capture button starts a real download to prove the whole path works.

Everything runs on your machine: the add-on talks to the desktop app over loopback only.

**Category** [REQUIRED]
Download Management

**Tags** [up to 10]
download-manager, download, video-download, blob, local, productivity

**License** [REQUIRED]
GNU General Public License v2.0 (`LICENSE`)

**Homepage URL** [RECOMMENDED]
https://mayanktaker.com

**Support site** [RECOMMENDED]
https://github.com/Mayanktaker/fetchflow/issues

**Support email** [RECOMMENDED]
mayanktaker@mayanktaker.com

**Privacy policy URL** [REQUIRED]
https://mayanktaker.com/privacy (source: [`docs/privacy.html`](docs/privacy.html))

**Version**
`9.1.23.0` — declared in `app/XDM/firefox-amo/manifest.json`; `version.env` is the single source of truth and `XDM.Tests/VersionConsistencyTests.cs` fails if the two drift.

**Compatibility**
Firefox 140+ (desktop) and Firefox for Android 142+ — the floor is set by `data_collection_permissions` in `browser_specific_settings`.

## Graphics & Assets

| Asset | Dimensions | Status | Filename |
|---|---|---|---|
| Add-on icon [REQUIRED] | 128×128 PNG | ✅ Ready | `app/XDM/firefox-amo/icon128.png` |
| Icon | 48×48 PNG | ✅ Ready | `app/XDM/firefox-amo/icon48.png` |
| Icon | 32×32 PNG | ✅ Ready | `app/XDM/firefox-amo/icon32.png` |
| Icon | 16×16 PNG | ✅ Ready | `app/XDM/firefox-amo/icon16.png` |
| Screenshot 1 [RECOMMENDED] | 1280×800 | 🟡 Needs export | popup with captured streams |
| Screenshot 2 [RECOMMENDED] | 1280×800 | 🟡 Needs export | FetchFlow desktop download list |

## Permissions Justification

Firefox names one permission differently from Chrome: this add-on requests `menus`
(Chrome's equivalent is `contextMenus`), and its code calls `browser.menus.*` to match.
It no longer requests `scripting` or `activeTab`, which nothing used.

| Permission | Type | Justification |
|---|---|---|
| `downloads` | permissions | Detect a browser download and cancel it so FetchFlow can fetch the file over multiple connections; also runs the popup's Test Capture self-test. |
| `webRequest` | permissions | Read response headers (`Content-Type`, `Content-Disposition`, length) to decide whether a response is a file the user's rules want FetchFlow to take over. |
| `webRequestBlocking` | permissions | Cancel an `Content-Disposition: attachment` response before Firefox writes it, so the browser's own save dialog never opens for a file FetchFlow is already fetching. Firefox still allows blocking `webRequest` in Manifest V3; Chrome does not, which is why the Chrome build intercepts a different way. |
| `webNavigation` | permissions | Observe page navigation and History-API transitions so a captured stream keeps the right referrer for the page it came from. |
| `tabs` | permissions | Read the originating tab's URL and title to name downloads and set a correct `Referer`, and to list the streams captured on the current tab in the popup. |
| `storage` | permissions | Keep the monitoring toggle, blocked-host exceptions, and the last configuration received from the desktop app, so they survive a browser restart. |
| `menus` | permissions | Add "Download with FetchFlow", "Download Image with FetchFlow" and "Download Blob Media with FetchFlow" to the right-click menu. |
| `cookies` | permissions | Attach the user's existing session cookies to the request FetchFlow makes, so downloads behind a login still work. Cookies go only to the local desktop app on `127.0.0.1`; nothing is sent to a third-party server. |
| `alarms` | permissions | Wake the background event page periodically. Firefox suspends event pages after ~30 seconds of idle and a pending download is not a waking event, so a sub-minute heartbeat keeps the listener alive. |
| `*://*/*` | host_permissions | The add-on cannot know in advance which site the user will download from; header inspection and takeover must work on any site the user visits. Requests to `127.0.0.1` (the desktop app) are excluded from every capture path. |

## Data Collection

AMO declares this in the manifest as `browser_specific_settings.gecko.data_collection_permissions.required = ["none"]`.

**Does the add-on collect or transmit user data?** No. Nothing leaves the machine.

| Data Type | Collected? | Transmitted Off-Device? | Purpose | Shared with Third Parties? |
|---|---|---|---|---|
| Personally identifiable info | No | No | N/A | No |
| Health info | No | No | N/A | No |
| Financial info | No | No | N/A | No |
| Authentication info | Optional (session cookies) | No — passed only to the local desktop client on `127.0.0.1` | Downloading a file the user already has access to | No |
| Personal communications | No | No | N/A | No |
| Location | No | No | N/A | No |
| Browsing activity | No | No | N/A | No |
| Website content | No | No | N/A | No |
| Technical and interaction data | No | No | N/A | No |

### Data Use Certification
- [x] Data is NOT sold to third parties
- [x] Data is NOT transferred for purposes unrelated to the add-on's core functionality
- [x] Data is NOT used for creditworthiness or lending purposes

## Distribution

**Visibility**: Public (AMO `listed` channel)
**Regions**: All regions
**Pricing**: Free
**Signing**: release builds sign through `web-ext sign --channel=listed`, which publishes the version to the public listing once Mozilla validates it. Without `MOZ_API_KEY` / `MOZ_API_SECRET` the pipeline attaches an unsigned XPI to the GitHub release instead.

## Developer Info

**Publisher Name** [REQUIRED]
Mayanktaker Computers & Web Development

**Contact Email** [REQUIRED]
mayanktaker@mayanktaker.com

**Source code**
https://github.com/Mayanktaker/fetchflow (`app/XDM/firefox-amo/`)

## Version History

| Version | Date | Changes |
|---|---|---|
| 9.1.23.0 | 2026-11-26 | Fixed the context-menu permission (`contextMenus` → `menus`), create menu items once per install instead of on every wake, dropped the unused `scripting`/`activeTab` permissions, removed dead pages, raised the minimum to Firefox 140. `web-ext lint`: 0 errors, 0 warnings. |
| 9.1.22.0 | 2026-11-06 | Version alignment with the desktop release; AMO data-collection declaration. |

## Review Notes

### Localhost IPC
The add-on is a bridge to the native FetchFlow Download Manager. It speaks WebSocket/HTTP to `127.0.0.1` on port `8597`, falling back through `8598`–`8603` when that port is taken. No remote server is contacted, and no relay or proxy is involved.

### Background event page
`background.scripts` runs as a Manifest V3 event page (Firefox does not use service workers for MV3). The `alarms` heartbeat documented above exists because Firefox suspends the event page after ~30 seconds and `downloads.onCreated` does not wake it; without the heartbeat a download started while the page is asleep would be missed.

### No media-platform circumvention
The add-on does not download or extract content from YouTube or any other platform's protected streams. Stream capture is limited to media the page itself is already playing in the user's browser, and the desktop app records the user's own request headers for that playback.

### Permissions that changed in 9.1.23.0
`contextMenus` → `menus` (correct name for the `browser.menus` namespace), and `scripting` + `activeTab` removed as unused. A reviewer comparing with 9.1.22.0 will see no new capability — only a rename and two removals.
