// © Mayanktaker Computers & Web Development | https://mayanktaker.com
"use strict";

// Content script: intercepts blob: URLs via injected page script,
// and intercepts blob DOWNLOAD ACTIONS at the DOM level before the
// browser's download manager can take over. Communicates with
// background (app.js) for chunked upload to FetchFlow.

console.log("[FetchFlow] blob-capture.js loaded (content script)");
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
    if (document.getElementById("fetchflow-blob-hook") || document.getElementById("xdm-blob-hook")) return;
    const script = document.createElement("script");
    script.id = "fetchflow-blob-hook";
    script.textContent = `(() => {
        if (window.__fetchflowBlobHooked || window.__xdmBlobHooked) return;
        window.__fetchflowBlobHooked = true;
        window.__xdmBlobHooked = true;

        // Helper: capture blob data in page context and post intent with data
        function captureAndPostIntent(blobUrl, filename) {
            fetch(blobUrl).then(r => r.blob()).then(blob => {
                const reader = new FileReader();
                reader.onload = () => {
                    window.postMessage({
                        type: "__fetchflow_blob_download_intent",
                        blobUrl, filename,
                        base64: reader.result.split(",")[1],
                        size: blob.size,
                        mime: blob.type || "application/octet-stream"
                    }, "*");
                };
                reader.readAsDataURL(blob);
            }).catch(() => {
                // Fallback: send intent without data
                window.postMessage({
                    type: "__fetchflow_blob_download_intent",
                    blobUrl, filename,
                    base64: null, size: 0, mime: ""
                }, "*");
            });
        }

        // Hook 1: Track blob URL creation
        const origCreate = URL.createObjectURL.bind(URL);
        URL.createObjectURL = function(obj) {
            const url = origCreate(obj);
            if (obj instanceof Blob) {
                try {
                    window.postMessage({
                        type: "__fetchflow_blob_created",
                        blobUrl: url,
                        size: obj.size,
                        mime: obj.type || "application/octet-stream"
                    }, "*");
                } catch(e) {}
            }
            return url;
        };

        // Hook 2: Intercept programmatic anchor.click() on blob: URLs
        const origAnchorClick = HTMLAnchorElement.prototype.click;
        HTMLAnchorElement.prototype.click = function() {
            if (this.href && this.href.startsWith("blob:")) {
                captureAndPostIntent(this.href, this.download || "");
                return; // suppress native browser download
            }
            return origAnchorClick.apply(this, arguments);
        };

        // Hook 3: Intercept window.open(blobUrl)
        const origOpen = window.open;
        window.open = function(url) {
            if (typeof url === "string" && url.startsWith("blob:")) {
                captureAndPostIntent(url, "");
                return null;
            }
            return origOpen.apply(this, arguments);
        };

        // Hook 4: Intercept dispatchEvent('click') on blob: anchors
        const origDispatch = EventTarget.prototype.dispatchEvent;
        EventTarget.prototype.dispatchEvent = function(event) {
            if (event && (event.type === "click" || event.type === "mousedown") &&
                this instanceof HTMLAnchorElement && this.href &&
                this.href.startsWith("blob:")) {
                captureAndPostIntent(this.href, this.download || "");
                return true;
            }
            return origDispatch.call(this, event);
        };
    })();`;
    (document.head || document.documentElement).appendChild(script);
    script.remove();
    console.log("[FetchFlow] Page-context hooks injected");
}

injectCreatorHook();

// --- Receive events from page context ---
window.addEventListener("message", (event) => {
    if (event.source !== window) return;
    const data = event.data;
    if (!data) return;

    if (data.type === "__fetchflow_blob_created" || data.type === "__xdm_blob_created") {
        blobRefs.set(data.blobUrl, {
            size: data.size, mime: data.mime, createdAt: Date.now()
        });
    }
    else if (data.type === "__fetchflow_blob_download_intent" || data.type === "__xdm_blob_download_intent") {
        // Blob download intent with pre-captured data (or fallback without)
        handleBlobDownloadIntent(data);
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
    console.log("[FetchFlow] DOM click on blob link:", href, filename);

    // Capture blob data immediately in content script context before URL is revoked
    fetch(href).then(r => r.blob()).then(blob => {
        const reader = new FileReader();
        reader.onload = () => {
            console.log("[FetchFlow] DOM blob captured:", blob.size, "bytes");
            sendBlobToBackground(href, filename, blob.type || "application/octet-stream", blob.size, reader.result.split(",")[1]);
        };
        reader.readAsDataURL(blob);
    }).catch(err => {
        console.log("[FetchFlow] DOM blob fetch failed, sending intent:", err);
        sendBlobToBackground(href, filename, "", 0, null);
    });
}, true);

// --- Handle blob download intent (with or without pre-captured data) ---
function handleBlobDownloadIntent(data) {
    const { blobUrl, filename, base64, size, mime } = data;
    if (base64) {
        console.log("[FetchFlow] Blob intent with data:", filename, size, "bytes");
        sendBlobToBackground(blobUrl, filename || deriveFilename(blobUrl, mime), mime, size, base64);
    } else {
        console.log("[FetchFlow] Blob intent without data, using ref:", blobUrl);
        const ref = blobRefs.get(blobUrl);
        const knownMime = ref?.mime || "application/octet-stream";
        sendBlobToBackground(blobUrl, filename || deriveFilename(blobUrl, knownMime), knownMime, ref?.size || 0, null);
    }
}

// --- Handle fetchflow-capture-blob requests from background (fallback re-fetch) ---
browser.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.type === "fetchflow-capture-blob" || message.type === "xdm-capture-blob") {
        console.log("[FetchFlow] Background requesting blob capture:", message.blobUrl);
        fetch(message.blobUrl).then(r => r.blob()).then(blob => {
            const reader = new FileReader();
            reader.onload = () => {
                sendResponse({
                    base64: reader.result.split(",")[1],
                    size: blob.size,
                    mime: blob.type || "application/octet-stream"
                });
            };
            reader.readAsDataURL(blob);
        }).catch(err => {
            console.log("[FetchFlow] Background blob capture failed:", err);
            sendResponse({ error: "Failed to capture blob: " + err.message });
        });
        return true; // async response
    }
    else if (message.type === "fetchflow-stream-result" || message.type === "xdm-stream-result") {
        // Background forwarded the streaming result — show in page console
        if (message.success) {
            console.log("[FetchFlow] ✅ Stream complete!", message.detail);
        } else {
            console.error("[FetchFlow] ❌ Stream FAILED:", message.error, message.detail);
        }
    }
});

// --- Send blob data to background for chunked upload ---
async function sendBlobToBackground(blobUrl, filename, mime, size, base64) {
    try {
        const msgType = base64 ? "fetchflow-blob-download-data" : "fetchflow-blob-download-intent";
        console.log("[FetchFlow] Sending to background:", msgType, filename, size, "bytes");
        const reply = await browser.runtime.sendMessage({
            type: msgType,
            blobUrl, filename, mime, size, base64
        });
        console.log("[FetchFlow] Background reply:", reply);
    } catch (e) {
        console.error("[FetchFlow] blob send error:", e.message || e);
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
