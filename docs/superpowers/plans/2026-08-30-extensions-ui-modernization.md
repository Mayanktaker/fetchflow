# XDM Browser Extensions Modernization — Implementation Plan (Phase 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the Chrome + Firefox extension surfaces (popup, confirm, error, disabled, register pages) into the unified XDM design language — charcoal surfaces, blue interactive accent, orange brand moments, rounded cards, auto dark/light — with zero behavioral change.

**Architecture:** Pure markup/CSS. `app/XDM/chrome-extension/` is the authored source of truth; Firefox gets byte-identical copies. No changes to background/content scripts, manifest, permissions, or messaging logic. The single bounded exception: `popup.js` swaps injected-element inline style strings for CSS classes (styling attributes only — no logic touched; documented per spec §6 intent).

**Tech Stack:** Vanilla HTML/CSS (MV3 extensions, Chrome + Firefox).

**Spec:** `docs/superpowers/specs/2026-08-30-xdm-design-system-modernization-design.md` (§3 tokens, §6 extensions) + `docs/design.md`.

## Global Constraints

- Chrome ↔ Firefox shared files must remain **byte-identical** (`diff -q` gate).
- Preserve these DOM id contracts exactly (JS depends on them):
  - `popup.html`: `content`, `list`, `table`, `format`, `clear`, `chk`
  - `confirm.html`: `size`, `btn-ok`, `btn-cancel`
  - `error.html`: `OpenLink`
  - `register.html`: `link` (note: two elements share this id today — keep both, do not "fix"), `content`
- Do not run `build_all.sh` (releases are explicit per AGENTS.md). Do not touch `manifest.json`, `app.js`, `connector.js`, `request-watcher.js`, `blob-capture.js`, `logger.js`, `main.js`, icons.
- Tokens (from `docs/design.md`): dark surfaces `#1d1d1d/#161616/#232323/#262626`, border `#404040`, text `#f4f4f4`, dim `#b4b4b4`, accent `#3584e4` hover `#5a9bea`, destructive `#e01b24` hover `#ef4a52`, brand `#f97316`, success `#4ade80`; light: `#fcfcfc/#ececec/#ffffff/#ffffff`, border `#cacaca`, text `#222222`, dim `#6b6b6b`, brand `#ea580c`, success `#16a34a`. Radii: sm 6 / md 8 / lg 10 / pill 999. Spacing 4/8/12/16/24.
- Blue = only interactive accent; orange only on the brand mark; destructive stays red.
- Font: `system-ui, -apple-system, "Segoe UI", sans-serif`.

---

### Task 1: `styles.css` — token foundation + component styles

**Files:**
- Modify: `app/XDM/chrome-extension/styles.css` (full rewrite)

**Interfaces:**
- Consumes: tokens above.
- Produces: every class used by Tasks 2–3 markup: `.popup`, `.popup-header`, `.brand-mark`, `.popup-title`, `.status-dot`, `.popup-body`, `.media-list`, `.media-table`, `.media-item`, `.media-item-title`, `.media-item-info`, `.toolbar-row`, `.btn`, `.btn-primary`, `.btn-flat`, `.btn-danger-text`, `.popup-footer`, `.container`, `.checkmark`, `.page`, `.page-card`, `.card-title`, `.info`, `.size`, `.btn-row`, `.error-msg`.

- [ ] **Step 1: Replace the entire file** with (full content):

```css
/*
 * XDM extension design system — mirrors docs/design.md (spec §6)
 * © Mayanktaker Computers & Web Development | https://mayanktaker.com
 *
 * Tokens: dark default; light via prefers-color-scheme.
 * Blue #3584e4 = only interactive accent. Orange #f97316 = brand mark only.
 */

:root {
  color-scheme: dark;
  --bg: #1d1d1d;
  --bg-sidebar: #161616;
  --bg-view: #232323;
  --bg-elevated: #262626;
  --border: #404040;
  --border-soft: #333333;
  --text: #f4f4f4;
  --text-dim: #b4b4b4;
  --text-faint: #8a8a8a;
  --accent: #3584e4;
  --accent-hover: #5a9bea;
  --accent-active: #2767b8;
  --destructive: #e01b24;
  --destructive-hover: #ef4a52;
  --destructive-text: #ef6a70;
  --brand-from: #f97316;
  --brand-to: #fb923c;
  --success: #4ade80;
  --r-sm: 6px;
  --r-md: 8px;
  --r-lg: 10px;
  --r-pill: 999px;
  --s1: 4px; --s2: 8px; --s3: 12px; --s4: 16px; --s5: 24px;
}

@media (prefers-color-scheme: light) {
  :root {
    color-scheme: light;
    --bg: #fcfcfc;
    --bg-sidebar: #ececec;
    --bg-view: #ffffff;
    --bg-elevated: #ffffff;
    --border: #cacaca;
    --border-soft: #e0e0e0;
    --text: #222222;
    --text-dim: #6b6b6b;
    --text-faint: #9a9a9a;
    --brand-from: #ea580c;
    --brand-to: #f97316;
    --success: #16a34a;
  }
}

html, body {
  padding: 0;
  margin: 0;
  font-family: system-ui, -apple-system, "Segoe UI", sans-serif;
  font-size: 14px;
  background: var(--bg);
  color: var(--text);
}

/* ===== Popup layout ===== */

body.popup {
  min-width: 400px;
}

.popup-header {
  display: flex;
  align-items: center;
  gap: var(--s2);
  padding: var(--s3) var(--s4);
  background: var(--bg-sidebar);
  border-bottom: 1px solid var(--border-soft);
}

.brand-mark {
  width: 22px;
  height: 22px;
  border-radius: var(--r-md);
  background: linear-gradient(135deg, var(--brand-from), var(--brand-to));
  color: #ffffff;
  font-weight: 700;
  font-size: 14px;
  line-height: 22px;
  text-align: center;
  user-select: none;
}

.brand-mark::after {
  content: "↓";
}

.popup-title {
  font-weight: 700;
  font-size: 14px;
}

.status-dot {
  margin-left: auto;
  width: 7px;
  height: 7px;
  border-radius: var(--r-pill);
  background: var(--success);
  box-shadow: 0 0 4px var(--success);
}

.popup-body {
  display: block;
}

.media-list {
  min-height: 200px;
  max-height: 400px;
  overflow: hidden;
  overflow-y: auto;
}

.media-table {
  width: 100%;
  background: var(--bg-view);
  margin: 0;
  padding: 0;
  border-collapse: collapse;
}

/* Rows injected by popup.js renderList() carry these classes */
.media-item {
  padding: var(--s3) var(--s4);
  display: flex;
  flex-direction: column;
  gap: var(--s1);
  border-bottom: 1px solid var(--border-soft);
}

.media-item:last-child {
  border-bottom: none;
}

.media-item-title {
  font-size: 14px;
  cursor: pointer;
  text-align: left;
  border: none;
  background: transparent;
  color: var(--text);
  padding: 0;
  font-family: inherit;
}

.media-item-title:hover {
  color: var(--accent-hover);
}

.media-item-info {
  font-size: 12px;
  color: var(--text-dim);
}

.toolbar-row {
  display: flex;
  gap: var(--s2);
  padding: var(--s2) var(--s3);
  border-top: 1px solid var(--border-soft);
}

.toolbar-row .btn {
  flex: 1;
}

.popup-footer {
  background: var(--bg-sidebar);
  border-top: 1px solid var(--border-soft);
  padding: var(--s2) var(--s4);
}

/* ===== Buttons ===== */

.btn {
  appearance: none;
  border: none;
  border-radius: var(--r-md);
  padding: var(--s2) var(--s4);
  font: inherit;
  font-weight: 600;
  cursor: pointer;
  color: var(--text);
  background: var(--bg-elevated);
  border: 1px solid var(--border-soft);
}

.btn:hover {
  background: var(--accent-hover);
  border-color: var(--accent-hover);
  color: #ffffff;
}

.btn-primary {
  background: var(--accent);
  border: 1px solid var(--accent-active);
  color: #ffffff;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.25);
}

.btn-primary:hover {
  background: var(--accent-hover);
  border-color: var(--accent);
}

.btn-flat {
  background: transparent;
  border: 1px solid transparent;
}

.btn-flat:hover {
  background: var(--accent);
  border-color: transparent;
  color: #ffffff;
}

.btn-danger-text {
  color: var(--destructive-text);
}

.btn-danger-text:hover {
  background: var(--destructive);
  border-color: transparent;
  color: #ffffff;
}

/* ===== Toggle (browser monitoring checkbox) ===== */

.container {
  display: flex;
  align-items: center;
  position: relative;
  padding-left: 30px;
  cursor: pointer;
  user-select: none;
  font-size: 14px;
}

.container input {
  position: absolute;
  opacity: 0;
  cursor: pointer;
  height: 0;
  width: 0;
}

.checkmark {
  position: absolute;
  left: 0;
  height: 17px;
  width: 17px;
  border-radius: var(--r-sm);
  background-color: var(--bg-elevated);
  border: 1px solid var(--border);
}

.container:hover input ~ .checkmark {
  border-color: var(--accent);
}

.container input:checked ~ .checkmark {
  background-color: var(--accent);
  border-color: var(--accent);
}

.checkmark:after {
  content: "";
  position: absolute;
  display: none;
}

.container input:checked ~ .checkmark:after {
  display: block;
}

.container .checkmark:after {
  left: 5px;
  top: 1px;
  width: 5px;
  height: 9px;
  border: solid #ffffff;
  border-width: 0 2px 2px 0;
  transform: rotate(45deg);
}

/* ===== Standalone pages (confirm / error / disabled / register) ===== */

body.page {
  min-height: 100vh;
  padding: var(--s5);
  box-sizing: border-box;
  background: var(--bg-sidebar);
}

.page-card {
  max-width: 400px;
  margin: 0 auto;
  background: var(--bg);
  border: 1px solid var(--border-soft);
  border-radius: var(--r-lg);
  padding: var(--s5);
}

.card-title {
  color: var(--text);
  margin: 0 0 var(--s3);
  font-size: 16px;
  font-weight: 700;
}

.info {
  font-size: 13px;
  color: var(--text-dim);
  margin: 0 0 var(--s4);
  line-height: 1.5;
}

.size {
  color: var(--accent-hover);
  font-weight: 700;
}

.btn-row {
  display: flex;
  gap: var(--s3);
}

.btn-row .btn {
  flex: 1;
}

/* ===== Error message ===== */

.error-msg {
  background: rgba(224, 27, 36, 0.12);
  border: 1px solid rgba(224, 27, 36, 0.35);
  border-left: 4px solid var(--destructive);
  border-radius: var(--r-md);
  padding: var(--s3) var(--s4);
  font-size: 14px;
  color: var(--text);
  margin: 0 0 var(--s4);
}

.error-msg p {
  margin: 0;
}

/* Register page links */
.page-card a {
  color: var(--accent-hover);
  font-weight: 600;
  text-decoration: none;
}

.page-card a:hover {
  text-decoration: underline;
}

.page-card > div {
  margin-bottom: var(--s3);
}
```

- [ ] **Step 2: Syntax sanity check**

Run: `grep -c "{" app/XDM/chrome-extension/styles.css && grep -c "}" app/XDM/chrome-extension/styles.css`
Expected: identical counts (balanced braces).

- [ ] **Step 3: Commit**

```bash
git add app/XDM/chrome-extension/styles.css
git commit -m "feat(ext): token-based design system stylesheet"
```

---

### Task 2: Popup — markup + styling-only JS edit

**Files:**
- Modify: `app/XDM/chrome-extension/popup.html` (full rewrite)
- Modify: `app/XDM/chrome-extension/popup.js` (`renderList` only — styling attributes, no logic)

**Interfaces:**
- Consumes: classes from Task 1.
- Produces: markup whose id set is exactly `content`, `list`, `table`, `format`, `clear`, `chk` (contract preserved).

- [ ] **Step 1: Replace `popup.html`** (full content):

```html
<!DOCTYPE html>
<html>

<head>
  <meta charset="utf-8">
  <script src="popup.js"></script>
  <link rel="stylesheet" href="styles.css">
</head>

<body class="popup">
  <header class="popup-header">
    <span class="brand-mark" aria-hidden="true"></span>
    <span class="popup-title">XDM</span>
    <span class="status-dot" title="Connected"></span>
  </header>
  <main id="content" class="popup-body">
    <div id="list" class="media-list">
      <table id="table" class="media-table"></table>
    </div>
    <div class="toolbar-row">
      <button id="format" class="btn btn-flat">More formats</button>
      <button id="clear" class="btn btn-flat btn-danger-text">Clear items</button>
    </div>
  </main>
  <footer class="popup-footer">
    <label class="container">Browser monitoring
      <input type="checkbox" id="chk">
      <span class="checkmark"></span>
    </label>
  </footer>
</body>

</html>
```

Note: `popup.js` sets `display:none`/`block` on `#content` inline at runtime — untouched, works as before.

- [ ] **Step 2: Styling-only edit in `popup.js` `renderList()`** (lines 44–60). Replace the three `setAttribute("style", ...)` blocks and the unused `border` variable so the method reads:

```js
    renderList(arr) {
        let table = document.getElementById("table");
        console.log("total element: " + arr.length);
        arr.forEach(listItem => {
            let text = listItem.text;

            let info = listItem.info;
            let id = listItem.id;

            let row = table.insertRow(0);
            let cell = row.insertCell(0);

            let div = document.createElement('div');
            div.className = 'media-item';

            let button = document.createElement('button');
            button.className = 'media-item-title';
            button.innerText = text;
            button.id = listItem.id;

            let p2 = document.createElement('span');
            p2.className = 'media-item-info';
            let node = document.createTextNode(info);
            p2.appendChild(node);

            div.appendChild(button);
            div.appendChild(p2);

            cell.appendChild(div);

            button.addEventListener('click', e => {
                chrome.runtime.sendMessage({ type: "vid", itemId: e.target.id });
            });
        });
    }
```

Everything else in `popup.js` stays byte-identical. Verify with `git diff app/XDM/chrome-extension/popup.js` — the diff must show ONLY the removed `let border = "";` line, three style-attribute lines replaced by three `className` lines, and nothing else.

- [ ] **Step 3: ID contract check**

Run: `grep -o 'id="[^"]*"' app/XDM/chrome-extension/popup.html | sort`
Expected exactly: `id="chk"`, `id="clear"`, `id="content"`, `id="format"`, `id="list"`, `id="table"`.

- [ ] **Step 4: Commit**

```bash
git add app/XDM/chrome-extension/popup.html app/XDM/chrome-extension/popup.js
git commit -m "feat(ext): popup markup in design language, classed rows"
```

---

### Task 3: Aux pages — confirm, error, disabled, register

**Files:**
- Modify: `app/XDM/chrome-extension/confirm.html` (full rewrite; moves its embedded `<style>` into shared stylesheet)
- Modify: `app/XDM/chrome-extension/error.html` (full rewrite)
- Modify: `app/XDM/chrome-extension/disabled.html` (full rewrite)
- Modify: `app/XDM/chrome-extension/register.html` (full rewrite)

**Interfaces:**
- Consumes: `.page`, `.page-card`, `.card-title`, `.info`, `.size`, `.btn-row`, `.btn`, `.btn-primary`, `.btn-flat`, `.error-msg`, `.popup-header`, `.brand-mark`, `.popup-title` (Task 1).
- Produces: id contracts preserved: confirm `size`/`btn-ok`/`btn-cancel`; error `OpenLink`; register `link` (×2) + `content`.

- [ ] **Step 1: Replace `confirm.html`** (full content):

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>XDM - Large Blob Download</title>
    <link rel="stylesheet" href="styles.css">
</head>
<body class="page">
    <div class="page-card">
        <h2 class="card-title">Large Blob Download</h2>
        <p class="info">This file (<span class="size" id="size"></span>) exceeds the configured blob size limit. Transfer it to XDM anyway?</p>
        <div class="btn-row">
            <button class="btn btn-primary" id="btn-ok">Download with XDM</button>
            <button class="btn btn-flat" id="btn-cancel">Cancel</button>
        </div>
    </div>
    <script src="confirm.js"></script>
</body>
</html>
```

- [ ] **Step 2: Replace `error.html`** (full content; `error.js` stays in `<head>` as before):

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <script src="error.js"></script>
    <link rel="stylesheet" href="styles.css">
</head>
<body class="page">
    <div class="page-card">
        <header class="popup-header" style="margin: -24px -24px 16px -24px; border-radius: 10px 10px 0 0;">
            <span class="brand-mark" aria-hidden="true"></span>
            <span class="popup-title">XDM</span>
        </header>
        <div class="error-msg">
            <p>Unable to connect with XDM, please make sure XDM is running</p>
        </div>
        <div class="btn-row">
            <button class="btn btn-primary" id="OpenLink">Launch XDM</button>
        </div>
    </div>
</body>
</html>
```

- [ ] **Step 3: Replace `disabled.html`** (full content):

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <link rel="stylesheet" href="styles.css">
</head>
<body class="page">
    <div class="page-card">
        <header class="popup-header" style="margin: -24px -24px 16px -24px; border-radius: 10px 10px 0 0;">
            <span class="brand-mark" aria-hidden="true"></span>
            <span class="popup-title">XDM</span>
        </header>
        <p class="info">Browser monitoring is disabled in the XDM application</p>
    </div>
</body>
</html>
```

- [ ] **Step 4: Replace `register.html`** (full content; both `id="link"` elements preserved — do not merge them):

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <script src="register.js"></script>
    <link rel="stylesheet" href="styles.css">
</head>
<body class="page">
    <div class="page-card">
        <header class="popup-header" style="margin: -24px -24px 16px -24px; border-radius: 10px 10px 0 0;">
            <span class="brand-mark" aria-hidden="true"></span>
            <span class="popup-title">XDM</span>
        </header>
        <div>Please register the extension with XDM</div>
        <div><a id="link" class="btn btn-primary" href="#">Register with XDM</a></div>
        <div id="content"></div>
        <div><a id="link" href="#">Once registered, please restart the browser</a></div>
    </div>
</body>
</html>
```

- [ ] **Step 5: ID contract check**

Run: `grep -o 'id="[^"]*"' app/XDM/chrome-extension/confirm.html app/XDM/chrome-extension/error.html app/XDM/chrome-extension/register.html app/XDM/chrome-extension/disabled.html`
Expected: confirm → `size`, `btn-ok`, `btn-cancel`; error → `OpenLink`; register → `link`, `content`, `link`; disabled → none.

- [ ] **Step 6: Commit**

```bash
git add app/XDM/chrome-extension/confirm.html app/XDM/chrome-extension/error.html app/XDM/chrome-extension/disabled.html app/XDM/chrome-extension/register.html
git commit -m "feat(ext): aux pages in unified design language"
```

---

### Task 4: Firefox sync + verification

**Files:**
- Modify (copy targets): `app/XDM/firefox-amo/styles.css`, `app/XDM/firefox-amo/app/styles.css`, `app/XDM/firefox-amo/popup.html`, `app/XDM/firefox-amo/app/popup.html`, `app/XDM/firefox-amo/popup.js`, `app/XDM/firefox-amo/app/popup.js`, `app/XDM/firefox-amo/confirm.html`, `app/XDM/firefox-amo/error.html`, `app/XDM/firefox-amo/app/error.html`, `app/XDM/firefox-amo/disabled.html`, `app/XDM/firefox-amo/app/disabled.html`, `app/XDM/firefox-amo/register.html`

**Interfaces:**
- Consumes: final Chrome files (Tasks 1–3).
- Produces: byte-identical Chrome↔Firefox file set.

Firefox layout note (verified): page files exist at root and under `app/` exactly as listed; `confirm.html` and `register.html` exist at root only. Copy each Chrome file to every Firefox path where that filename already exists — no new files, no deletions.

- [ ] **Step 1: Copy**

```bash
cd /mnt/Development/Web_Projects/Xdman-Wayland/xdm/app/XDM
for f in styles.css popup.html popup.js error.html disabled.html; do
  cp chrome-extension/$f firefox-amo/$f
  cp chrome-extension/$f firefox-amo/app/$f
done
cp chrome-extension/confirm.html firefox-amo/confirm.html
cp chrome-extension/register.html firefox-amo/register.html
```

- [ ] **Step 2: Byte-identity gate**

```bash
cd /mnt/Development/Web_Projects/Xdman-Wayland/xdm/app/XDM
ok=1
for p in firefox-amo/styles.css firefox-amo/app/styles.css firefox-amo/popup.html firefox-amo/app/popup.html firefox-amo/popup.js firefox-amo/app/popup.js firefox-amo/confirm.html firefox-amo/error.html firefox-amo/app/error.html firefox-amo/disabled.html firefox-amo/app/disabled.html firefox-amo/register.html; do
  diff -q "chrome-extension/$(basename $p)" "$p" || ok=0
done
[ $ok -eq 1 ] && echo "ALL IDENTICAL" || echo "MISMATCH FOUND"
```

Expected: `ALL IDENTICAL`.

- [ ] **Step 3: Visual verification (static pages)**

Using the agent-browser skill (or any headless browser), open and screenshot:
- `file:///mnt/Development/Web_Projects/Xdman-Wayland/xdm/app/XDM/chrome-extension/confirm.html`
- `file:///mnt/Development/Web_Projects/Xdman-Wayland/xdm/app/XDM/chrome-extension/error.html`
- `file:///mnt/Development/Web_Projects/Xdman-Wayland/xdm/app/XDM/chrome-extension/disabled.html`
- `file:///mnt/Development/Web_Projects/Xdman-Wayland/xdm/app/XDM/chrome-extension/register.html`

Expected each: charcoal centered card (lg radius) on tinted backdrop, orange gradient brand mark in the header, blue primary button (confirm/error/register), red-tinted error card (error page), system font. `chrome.runtime` console errors are expected on `file://` — page rendering is what matters. Toggle the browser's dark/light emulation and confirm both palettes render (auto `prefers-color-scheme`).

Popup (`popup.html`) cannot be exercised outside the extension runtime — Mayank loads the unpacked extension in Chrome and Firefox for the final check (header + footer render; media rows appear when a video page is open).

- [ ] **Step 4: Regression sweep**

Run: `~/.dotnet8/dotnet test app/XDM/XDM.Tests/XDM.Tests.csproj`
Expected: green (extension JS blob-capture/JSON tests unaffected).

- [ ] **Step 5: Commit**

```bash
git add app/XDM/firefox-amo
git commit -m "feat(ext): sync firefox surfaces to unified design language"
```

---

## Final acceptance (both extensions)

- [ ] All 12 Firefox copies byte-identical to Chrome sources (`diff -q` gate green).
- [ ] Screenshots show the unified language: charcoal surfaces, orange brand mark, blue interactive accent, lg-radius cards, pill/smd radii on controls.
- [ ] Dark and light both render correctly (browser theme emulation).
- [ ] Mayank confirms the live popup in Chrome + Firefox (unpacked) — toggles, media list, More formats, Clear items all function.
- [ ] `git status` clean; no manifest/script-logic changes in the diff.
