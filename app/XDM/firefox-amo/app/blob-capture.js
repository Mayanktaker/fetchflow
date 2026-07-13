// © Mayanktaker Computers & Web Development | https://mayanktaker.com
"use strict";

// Content script: intercepts blob: URLs via injected page script,
// and intercepts blob DOWNLOAD ACTIONS at the DOM level before the
// browser's download manager can take over. Communicates with
// background (app.js) for chunked upload to XDM.

const BLOB_TTL_MS = 5 * 60 * 1000;
const blobRefs = new Map();

// --- Periodic cleanup of stale blob refs ---
setInterval(() => {
    const now = Date.now();
    for (const [url, ref] of blobRefs) {
        if (now - ref.createdAt > BLOB_TTL_MS) blobRefs.delete(url);
    }
}, 60_000);

// --- Inject page-context hooks: URL.createObjectURL + anchor.click + window.open ---
function injectCreatorHook() {
    if (document.getElementById("xdm-blob-hook")) return;
    const script = document.createElement("script");
    script.id = "xdm-blob-hook";
    script.textContent = `(() => {
        if (window.__xdmBlobHooked) return;
        window.__xdmBlobHooked = true;

        // Hook 1: Track blob URL creation
        const origCreate = URL.createObjectURL.bind(URL);
        URL.createObjectURL = function(obj) {
            const url = origCreate(obj);
            if (obj instanceof Blob) {
                try {
                    window.postMessage({
                        type: "__xdm_blob_created",
                        blobUrl: url,
                        size: obj.size,
                        mime: obj.type || "application/octet-stream"
                    }, "*");
                } catch(e) {}
            }
            return url;
        };

        // Hook 2: Intercept programmatic anchor.click() on blob: URLs
        // This catches the common pattern: const a = document.createElement('a');
        // a.href = blobUrl; a.download = 'file.mp4'; a.click();
        const origAnchorClick = HTMLAnchorElement.prototype.click;
        HTMLAnchorElement.prototype.click = function() {
            if (this.href && this.href.startsWith("blob:")) {
                window.postMessage({
                    type: "__xdm_blob_download_intent",
                    blobUrl: this.href,
                    filename: this.download || ""
                }, "*");
                return; // suppress native browser download
            }
            return origAnchorClick.apply(this, arguments);
        };

        // Hook 3: Intercept window.open(blobUrl)
        const origOpen = window.open;
        window.open = function(url) {
            if (typeof url === "string" && url.startsWith("blob:")) {
                window.postMessage({
                    type: "__xdm_blob_download_intent",
                    blobUrl: url,
                    filename: ""
                }, "*");
                return null;
            }
            return origOpen.apply(this, arguments);
        };

        // Hook 4: Intercept dispatchEvent('click') on blob: anchors
        // Many frameworks (React, Vue) use dispatchEvent instead of .click()
        const origDispatch = EventTarget.prototype.dispatchEvent;
        EventTarget.prototype.dispatchEvent = function(event) {
            if (event && (event.type === "click" || event.type === "mousedown") &&
                this instanceof HTMLAnchorElement && this.href &&
                this.href.startsWith("blob:")) {
                window.postMessage({
                    type: "__xdm_blob_download_intent",
                    blobUrl: this.href,
                    filename: this.download || ""
                }, "*");
                return true; // suppress native download
            }
            return origDispatch.call(this, event);
        };
    })();`;
    (document.head || document.documentElement).appendChild(script);
    script.remove();
}

injectCreatorHook();

// --- Receive events from page context ---
window.addEventListener("message", (event) => {
    if (event.source !== window) return;
    const data = event.data;
    if (!data) return;

    if (data.type === "__xdm_blob_created") {
        blobRefs.set(data.blobUrl, {
            size: data.size, mime: data.mime, createdAt: Date.now()
        });
    }
    else if (data.type === "__xdm_blob_download_intent") {
        // Programmatic blob download detected (anchor.click or window.open)
        handleBlobDownloadIntent(data.blobUrl, data.filename);
    }
});

// --- DOM-level click interceptor: catch user clicks on <a href="blob:..."> ---
// Uses capture phase so we run BEFORE the page's own click handlers.
document.addEventListener("click", (e) => {
    const link = e.target?.closest?.("a[href]");
    if (!link) return;
    const href = link.href || link.getAttribute("href");
    if (!href || !href.startsWith("blob:")) return;

    e.preventDefault();
    e.stopPropagation();
    const filename = link.getAttribute("download") || link.textContent?.trim() || "";
    handleBlobDownloadIntent(href, filename);
}, true);

// --- Handle a blob download intent (from click or programmatic trigger) ---
function handleBlobDownloadIntent(blobUrl, filename) {
    const ref = blobRefs.get(blobUrl);
    if (ref) {
        // Known blob — send directly to background for XDM transfer
        sendBlobDownload(blobUrl, filename || deriveFilename(blobUrl, ref.mime), ref.mime, ref.size);
    } else {
        // Unknown blob URL — still try to capture (page may have created it
        // before our hook was injected). The background will re-fetch it.
        sendBlobDownload(blobUrl, filename || "blob-download.bin", "application/octet-stream", 0);
    }
}

// --- Send blob download request to background for chunked upload ---
async function sendBlobDownload(blobUrl, filename, mime, size) {
    try {
        browser.runtime.sendMessage({
            type: "xdm-blob-download-intent",
            blobUrl, filename, mime, size
        });
    } catch (e) {
        console.log("[XDM] blob download intent send error:", e);
    }
}

// --- Listen for messages from background script ---
browser.runtime.onMessage.addListener((msg, sender, sendResponse) => {
    if (msg.type === "xdm-capture-blob") {
        captureBlobFromTab(msg.blobUrl, msg.filename).then(sendResponse);
        return true;
    }
});

// --- Re-fetch blob bytes from page context and return directly ---
async function captureBlobFromTab(blobUrl, fallbackFilename) {
    const ref = blobRefs.get(blobUrl);
    const tabId = await getCurrentTabId();
    if (!tabId) return { error: "no_tab" };

    try {
        const results = await browser.scripting.executeScript({
            target: { tabId },
            world: "MAIN",
            func: (url) => {
                return new Promise((resolve) => {
                    fetch(url).then(r => r.blob()).then(blob => {
                        const reader = new FileReader();
                        reader.onload = () => resolve({
                            ok: true, data: reader.result,
                            size: blob.size, mime: blob.type
                        });
                        reader.onerror = () => resolve({ error: "read_failed" });
                        reader.readAsDataURL(blob);
                    }).catch(() => resolve({ error: "fetch_failed" }));
                });
            },
            args: [blobUrl]
        });

        const result = results?.[0]?.result;
        if (!result || result.error) return { error: result?.error || "execute_failed" };

        const base64 = result.data.split(",")[1];
        const mime = result.mime || ref?.mime || "application/octet-stream";
        const size = result.size || ref?.size || 0;
        const filename = fallbackFilename || deriveFilename(blobUrl, mime);

        // Return data directly — background receives this via sendResponse
        return { base64, filename, mime, size };
    } catch (e) {
        return { error: "exception: " + e.message };
    }
}

function deriveFilename(blobUrl, mime) {
    try {
        const u = new URL(blobUrl);
        const uuid = u.pathname.replace(/^\//, "");
        return (uuid.slice(0, 8) || "blob") + mimeExtension(mime);
    } catch {
        return "blob-download" + mimeExtension(mime);
    }
}

function mimeExtension(mime) {
    const map = {
        "image/png": ".png", "image/jpeg": ".jpg", "image/webp": ".webp",
        "image/gif": ".gif", "video/mp4": ".mp4", "video/webm": ".webm",
        "audio/mpeg": ".mp3", "audio/ogg": ".ogg", "audio/wav": ".wav",
        "application/pdf": ".pdf", "application/zip": ".zip",
        "application/octet-stream": ".bin"
    };
    return map[mime] || "";
}

async function getCurrentTabId() {
    const [tab] = await browser.tabs.query({ active: true, currentWindow: true });
    return tab?.id || null;
}
