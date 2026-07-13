// © Mayanktaker Computers & Web Development | https://mayanktaker.com
"use strict";

// Content script: intercepts blob: URLs via an injected page script,
// communicates with background (app.js) for chunked upload to XDM.

const BLOB_TTL_MS = 5 * 60 * 1000;

// Map<blobUrl, {blob, filename, mime, createdAt}>
const blobRefs = new Map();

// Periodic cleanup of stale blob refs to prevent memory leaks
setInterval(() => {
    const now = Date.now();
    for (const [url, ref] of blobRefs) {
        if (now - ref.createdAt > BLOB_TTL_MS) {
            blobRefs.delete(url);
        }
    }
}, 60_000);

// --- Inject page-context script to hook URL.createObjectURL ---
function injectCreatorHook() {
    if (document.getElementById("xdm-blob-hook")) return;
    const script = document.createElement("script");
    script.id = "xdm-blob-hook";
    script.textContent = `(() => {
        if (window.__xdmBlobHooked) return;
        window.__xdmBlobHooked = true;
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
    })();`;
    (document.head || document.documentElement).appendChild(script);
    script.remove();
}

// Run at document_start (before page scripts execute)
injectCreatorHook();

// Re-inject after dynamic document rewrites (e.g. SPA navigations)
new MutationObserver(() => {
    if (!document.getElementById("xdm-blob-hook")) injectCreatorHook();
}).observe(document.documentElement, { childList: true });

// --- Receive blob-creation events from page context ---
window.addEventListener("message", (event) => {
    if (event.source !== window) return;
    const data = event.data;
    if (!data || data.type !== "__xdm_blob_created") return;

    // Store ref with a 5-min TTL; also retain the Blob handle from the page
    blobRefs.set(data.blobUrl, {
        blob: null,       // Blob lives in page context; we re-fetch via tab
        size: data.size,
        mime: data.mime,
        createdAt: Date.now()
    });
});

// --- Listen for messages from background script ---
chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
    if (msg.type === "xdm-capture-blob") {
        captureBlobFromTab(msg.blobUrl, msg.filename).then(sendResponse);
        return true; // async response
    }
});

async function captureBlobFromTab(blobUrl, fallbackFilename) {
    const ref = blobRefs.get(blobUrl);
    if (!ref) return { error: "blob_ref_not_found" };

    // Re-fetch the blob from the page context (page still holds the Blob)
    const tabId = await getCurrentTabId();
    if (!tabId) return { error: "no_tab" };

    try {
        const results = await chrome.scripting.executeScript({
            target: { tabId },
            world: "MAIN",
            func: (url) => {
                return new Promise((resolve) => {
                    fetch(url).then(r => r.blob()).then(blob => {
                        const reader = new FileReader();
                        reader.onload = () => resolve({
                            ok: true,
                            data: reader.result, // data:...base64
                            size: blob.size,
                            mime: blob.type
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

        // Decode base64 to raw bytes via DataTransfer trick
        const dataUrl = result.data;
        const base64 = dataUrl.split(",")[1];
        const mime = result.mime || ref.mime || "application/octet-stream";
        const size = result.size || ref.size;

        // Derive filename
        const filename = fallbackFilename || deriveFilename(blobUrl, mime);

        // Send raw binary to background for chunked upload
        return await sendBlobToBackground(base64, filename, mime, size, blobUrl);
    } catch (e) {
        return { error: "exception: " + e.message };
    }
}

async function sendBlobToBackground(base64, filename, mime, size, blobUrl) {
    return new Promise((resolve) => {
        chrome.runtime.sendMessage({
            type: "xdm-blob-download",
            blobUrl,
            filename,
            mime,
            size,
            base64
        }, (response) => {
            resolve(response || { error: "no_response" });
        });
    });
}

function deriveFilename(blobUrl, mime) {
    try {
        const u = new URL(blobUrl);
        const uuid = u.pathname.replace(/^\//, "");
        const ext = mimeExtension(mime);
        return (uuid.slice(0, 8) || "blob") + ext;
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
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    return tab?.id || null;
}
