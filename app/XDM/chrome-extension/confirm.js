// © Mayanktaker Computers & Web Development | https://mayanktaker.com
"use strict";

// Script for the oversized-blob confirmation dialog

const params = new URLSearchParams(window.location.search);
const size = parseInt(params.get("size") || "0", 10);
const blobUrl = params.get("blobUrl") || "";
const filename = params.get("filename") || "";
const mime = params.get("mime") || "";
const tabId = parseInt(params.get("tabId") || "-1", 10);

function formatSize(bytes) {
    if (bytes < 1024) return bytes + " B";
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + " KB";
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + " MB";
    return (bytes / (1024 * 1024 * 1024)).toFixed(2) + " GB";
}

document.getElementById("size").textContent = formatSize(size);

document.getElementById("btn-ok").addEventListener("click", () => {
    chrome.runtime.sendMessage({
        type: "xdm-blob-download-confirmed",
        blobUrl, filename, mime, size, tabId: tabId + ""
    });
    window.close();
});

document.getElementById("btn-cancel").addEventListener("click", () => {
    window.close();
});
