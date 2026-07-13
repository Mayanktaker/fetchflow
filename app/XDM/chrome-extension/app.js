// © Mayanktaker Computers & Web Development | https://mayanktaker.com
"use strict";
import Logger from './logger.js';
import RequestWatcher from './request-watcher.js';
import Connector from './connector.js';

const BLOB_CHUNK_SIZE = 512 * 1024; // 512 KiB per chunk — keeps SW memory bounded
const DEFAULT_BLOB_MAX_BYTES = 256 * 1024 * 1024; // 256 MiB default cap

export default class App {
    constructor() {
        this.logger = new Logger();
        this.videoList = [];
        this.blockedHosts = [];
        this.fileExts = [];
        this.requestWatcher = new RequestWatcher(this.onRequestDataReceived.bind(this));
        this.tabsWatcher = [];
        this.userDisabled = false;
        this.appEnabled = false;
        this.blobMaxBytes = DEFAULT_BLOB_MAX_BYTES;
        this.onDownloadCreatedCallback = this.onDownloadCreated.bind(this);
        this.onDeterminingFilenameCallback = this.onDeterminingFilename.bind(this);
        this.onTabUpdateCallback = this.onTabUpdate.bind(this);
        this.activeTabId = -1;
        this.connector = new Connector(this.onMessage.bind(this), this.onDisconnect.bind(this));
    }

    start() {
        this.logger.log("starting...");
        // Load cached config to prevent missing requests on wakeup
        chrome.storage.local.get("xdmConfig", (res) => {
            if (res.xdmConfig) {
                this.updateConfig(res.xdmConfig);
            }
        });
        this.starAppConnector();
        this.register();
        this.logger.log("started.");
    }

    starAppConnector() {
        this.connector.connect();
    }

    onMessage(msg) {
        this.logger.log("message from XDM");
        this.logger.log(msg);
        chrome.storage.local.set({ "xdmConfig": msg });
        this.updateConfig(msg);
    }

    updateConfig(msg) {
        this.appEnabled = msg.enabled === true;
        this.fileExts = msg.fileExts;
        this.blockedHosts = msg.blockedHosts;
        this.tabsWatcher = msg.tabsWatcher;
        this.videoList = msg.videoList;
        if (msg.blobMaxBytes && msg.blobMaxBytes > 0) {
            this.blobMaxBytes = msg.blobMaxBytes;
        }
        this.requestWatcher.updateConfig({
            blockedHosts: msg.blockedHosts,
            fileExts: msg.fileExts,
            mediaExts: msg.requestFileExts,
            matchingHosts: msg.matchingHosts,
            mediaTypes: msg.mediaTypes
        });
        this.updateActionIcon();
    }

    onDisconnect() {
        this.logger.log("Disconnected from native host!");
        this.logger.log("Disconnected...");
        this.updateActionIcon();
    }

    isMonitoringEnabled() {
        this.logger.log(this.appEnabled + " " + this.userDisabled);
        return this.appEnabled === true && this.userDisabled === false;
    }

    onRequestDataReceived(data) {
        //Streaming video data received, send to native messaging application
        this.logger.log("onRequestDataReceived");
        this.logger.log(data);
        this.isMonitoringEnabled() && this.connector.isConnected() && this.connector.postMessage("/media", data);
    }

    onDeterminingFilename(download, suggest) {
        this.logger.log("onDeterminingFilename");
        if (!this.isMonitoringEnabled()) {
            return;
        }
        this.logger.log(download);
        let url = download.finalUrl || download.url;
        this.logger.log(url);

        // Blob URL interception: cancel browser DL and stream via blob-capture.js
        if (this.isBlobUrl(url)) {
            chrome.downloads.cancel(
                download.id,
                () => chrome.downloads.erase({ id: download.id })
            );
            const filename = download.filename || this.deriveBlobFilename(url, download.mime);
            this.startBlobTransfer(url, filename, download.mime, download.tabId);
            return;
        }

        if (this.isMonitoringEnabled() && this.shouldTakeOver(url, download.filename)) {
            chrome.downloads.cancel(
                download.id,
                () => chrome.downloads.erase({ id: download.id })
            );
            let referrer = download.referrer;
            if (!referrer && download.finalUrl !== download.url) {
                referrer = download.url;
            }
            this.triggerDownload(url, download.filename,
                referrer, download.fileSize, download.mime);
        }
    }

    onDownloadCreated(download) {
        this.logger.log("onDownloadCreated");
        this.logger.log(download);
    }

    onTabUpdate(tabId, changeInfo, tab) {
        if (!this.isMonitoringEnabled()) {
            return;
        }
        // Trigger on BOTH url and title changes — YouTube SPA uses History API
        let tabUrl = changeInfo.url || tab.url;
        let tabTitle = changeInfo.title || tab.title || "";
        if (changeInfo.url || changeInfo.title) {
            if (this.tabsWatcher &&
                this.tabsWatcher.find(t => tabUrl.indexOf(t) > 0)) {
                // Deduplicate — don't re-send the same URL
                if (this.lastSentTabUrl === tabUrl && !changeInfo.title) {
                    return;
                }
                this.lastSentTabUrl = tabUrl;
                this.logger.log("Tab changed: " + tabTitle + " => " + tabUrl);
                // Send tab URL and ID to native host for media processing
                try {
                    this.connector.postMessage("/tab-update", {
                        tabUrl: tabUrl,
                        tabTitle: tabTitle,
                        tabId: tabId + ""
                    });
                } catch (ex) {
                    console.log(ex);
                }
            }
        }
    }

    // Handle SPA navigation via History API (pushState/replaceState)
    onHistoryStateUpdated(details) {
        if (!this.isMonitoringEnabled()) {
            return;
        }
        if (details.frameId !== 0) return; // Only top-level frame
        let url = details.url;
        if (this.tabsWatcher &&
            this.tabsWatcher.find(t => url.indexOf(t) > 0)) {
            // Deduplicate — don't re-send the same URL
            if (this.lastSentTabUrl === url) {
                return;
            }
            this.lastSentTabUrl = url;
            this.logger.log("SPA nav: " + url);
            try {
                this.connector.postMessage("/tab-update", {
                    tabUrl: url,
                    tabTitle: "",
                    tabId: details.tabId + ""
                });
            } catch (ex) {
                console.log(ex);
            }
        }
    }


    register() {
        chrome.downloads.onCreated.addListener(
            this.onDownloadCreatedCallback
        );
        chrome.downloads.onDeterminingFilename.addListener(
            this.onDeterminingFilenameCallback
        );
        chrome.tabs.onUpdated.addListener(
            this.onTabUpdateCallback
        );
        // SPA navigation detection via History API (YouTube, etc.)
        if (chrome.webNavigation && chrome.webNavigation.onHistoryStateUpdated) {
            chrome.webNavigation.onHistoryStateUpdated.addListener(
                this.onHistoryStateUpdated.bind(this)
            );
        }
        chrome.runtime.onMessage.addListener(this.onPopupMessage.bind(this));
        this.requestWatcher.register();
        this.attachContextMenu();
        chrome.tabs.onActivated.addListener(this.onTabActivated.bind(this));
    }

    isSupportedProtocol(url) {
        if (!url) return false;
        let u = new URL(url);
        return u.protocol === 'http:' || u.protocol === 'https:';
    }

    isBlobUrl(url) {
        if (!url) return false;
        try {
            let u = new URL(url);
            return u.protocol === 'blob:';
        } catch { return false; }
    }

    deriveBlobFilename(blobUrl, mime) {
        try {
            let u = new URL(blobUrl);
            let uuid = u.pathname.replace(/^\//, "");
            let ext = this.mimeToExt(mime);
            return (uuid.slice(0, 8) || "blob") + ext;
        } catch {
            return "blob-download" + this.mimeToExt(mime);
        }
    }

    mimeToExt(mime) {
        if (!mime) return "";
        const map = {
            "image/png": ".png", "image/jpeg": ".jpg", "image/webp": ".webp",
            "image/gif": ".gif", "video/mp4": ".mp4", "video/webm": ".webm",
            "audio/mpeg": ".mp3", "audio/ogg": ".ogg", "audio/wav": ".wav",
            "application/pdf": ".pdf", "application/zip": ".zip",
            "application/octet-stream": ".bin"
        };
        return map[mime] || "";
    }

    startBlobTransfer(blobUrl, filename, mime, tabId) {
        if (!this.connector.isConnected()) {
            this.logger.log("Cannot transfer blob: not connected to XDM");
            return;
        }
        const size = 0; // unknown from downloads API for blobs; content script will report real size
        if (this.blobMaxBytes && size > this.blobMaxBytes) {
            this.promptBlobConfirm(blobUrl, filename, mime, size, tabId);
            return;
        }
        this.captureAndStreamBlob(blobUrl, filename, mime, tabId);
    }

    promptBlobConfirm(blobUrl, filename, mime, size, tabId) {
        const params = new URLSearchParams({
            blobUrl, filename, mime, size: size + "", tabId: (tabId || -1) + ""
        });
        chrome.tabs.create({ url: "confirm.html?" + params.toString() });
    }

    async captureAndStreamBlob(blobUrl, filename, mime, tabId) {
        this.logger.log("Capturing blob: " + blobUrl);
        // Ask the content script to re-fetch the blob from the page context
        const tabIds = tabId ? [parseInt(tabId, 10)] : [];
        if (tabIds.length === 0) {
            const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
            if (tab?.id) tabIds.push(tab.id);
        }
        if (tabIds.length === 0) {
            this.logger.log("No active tab for blob capture");
            return;
        }
        try {
            const response = await chrome.tabs.sendMessage(tabIds[0], {
                type: "xdm-capture-blob",
                blobUrl,
                filename
            });
            if (response?.error) {
                this.logger.log("Blob capture failed: " + response.error);
                return;
            }
            if (response?.base64) {
                await this.streamBlobToXdm(response.base64, filename, response.mime || mime, response.size || 0, blobUrl);
            }
        } catch (e) {
            this.logger.log("Blob capture message error: " + e.message);
        }
    }

    async streamBlobToXdm(base64Data, filename, mime, size, blobUrl) {
        this.logger.log("Streaming blob to XDM: " + filename + " (" + size + " bytes)");
        const raw = atob(base64Data);
        const bytes = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);

        const totalChunks = Math.ceil(bytes.length / BLOB_CHUNK_SIZE);
        const transferId = crypto.randomUUID();

        // Track active blob transfers for progress badge
        this.activeBlobTransfers = (this.activeBlobTransfers || 0) + 1;
        this.updateBlobBadge();

        for (let i = 0; i < totalChunks; i++) {
            const start = i * BLOB_CHUNK_SIZE;
            const end = Math.min(start + BLOB_CHUNK_SIZE, bytes.length);
            const chunk = bytes.slice(start, end);

            const headers = {
                "X-Blob-Transfer-Id": transferId,
                "X-Chunk-Index": i + "",
                "X-Total-Chunks": totalChunks + "",
                "X-Filename": filename,
                "X-Mime": mime || "",
                "X-Total-Size": size + "",
                "X-Blob-Url": blobUrl
            };

            try {
                const result = await this.connector.postBlobChunk(headers, chunk);
                if (result && result.error) {
                    this.logger.log("Blob chunk error: " + result.error);
                    this.activeBlobTransfers = Math.max(0, (this.activeBlobTransfers || 0) - 1);
                    this.updateBlobBadge();
                    return;
                }
                // Update progress badge with percentage
                this.updateBlobProgressBadge(i + 1, totalChunks);
            } catch (e) {
                this.logger.log("Blob chunk POST failed: " + e.message);
                this.activeBlobTransfers = Math.max(0, (this.activeBlobTransfers || 0) - 1);
                this.updateBlobBadge();
                return;
            }
        }
        this.logger.log("Blob stream complete: " + transferId);
        this.activeBlobTransfers = Math.max(0, (this.activeBlobTransfers || 0) - 1);
        this.updateBlobBadge();
    }

    // Show blob transfer progress in the action badge
    updateBlobProgressBadge(chunk, total) {
        if (!this.activeBlobTransfers) return;
        const pct = Math.round((chunk / total) * 100);
        chrome.action.setBadgeText({ text: pct + "%" });
        chrome.action.setBadgeBackgroundColor({ color: "#ff6b35" });
    }

    // Restore the normal badge when blob transfers complete
    updateBlobBadge() {
        if (!this.activeBlobTransfers) {
            // Reset to normal badge state
            this.updateActionIcon();
        }
    }

    shouldTakeOver(url, file) {
        let u = new URL(url);
        if (!this.isSupportedProtocol(url)) {
            return false;
        }
        let hostName = u.host;
        if (this.blockedHosts.find(item => hostName.indexOf(item) >= 0)) {
            return false;
        }
        let path = file || u.pathname;
        let upath = path.toUpperCase();
        if (this.fileExts.find(ext => upath.endsWith(ext))) {
            return true;
        }
        return false;
    }

    updateActionIcon() {
        chrome.action.setIcon({ path: this.getActionIcon() });
        let vc = "";
        if (this.videoList && this.videoList.length > 0) {
            let len = this.videoList.length;
            if (len > 0) {
                vc = len + "";
            }
        }
        // if (this.videoList && this.videoList.length > 0) {
        //     let len = this.videoList.filter(vid => {
        //         if (!vid.tabId) {
        //             return true;
        //         }
        //         if (vid.tabId == '-1') {
        //             return true;
        //         }
        //         return (vid.tabId == this.activeTabId);
        //     }).length;
        //     if (len > 0) {
        //         vc = len + "";
        //     }
        // }
        chrome.action.setBadgeText({ text: vc });
        if (!this.connector.isConnected()) {
            this.logger.log("Not connected...");
            chrome.action.setPopup({ popup: "./error.html" });
            return;
        }
        if (!this.appEnabled) {
            chrome.action.setPopup({ popup: "./disabled.html" });
            return;
        }
        else {
            chrome.action.setPopup({ popup: "./popup.html" });
            return;
            // if (this.videoList && this.videoList.length > 0) {
            //     chrome.action.setBadgeText({ text: this.videoList.length + "" });
            // }
        }
    }

    getActionIconName(icon) {
        return this.isMonitoringEnabled() ? icon + ".png" : icon + "-mono.png";
    }

    getActionIcon() {
        return {
            "16": this.getActionIconName("icon16"),
            "48": this.getActionIconName("icon48"),
            "128": this.getActionIconName("icon128")
        }
    }

    triggerDownload(url, file, referer, size, mime) {
        chrome.cookies.getAll({ "url": url }, cookies => {
            let cookieStr = undefined;
            if (cookies) {
                cookieStr = cookies.map(cookie => cookie.name + "=" + cookie.value).join("; ");
            }
            let requestHeaders = { "User-Agent": [navigator.userAgent] };
            if (referer) {
                requestHeaders["Referer"] = [referer];
            }
            let responseHeaders = {};
            if (size) {
                let fz = +size;
                if (fz > 0) {
                    responseHeaders["Content-Length"] = [fz];
                }
            }
            if (mime) {
                responseHeaders["Content-Type"] = [mime];
            }
            let data = {
                url: url,
                cookie: cookieStr,
                requestHeaders: requestHeaders,
                responseHeaders: responseHeaders,
                filename: file,
                fileSize: size,
                mimeType: mime
            };
            this.logger.log(data);
            this.connector.postMessage("/download", data);
        });
    }

    diconnect() {
        this.onDisconnect();
    }

    onPopupMessage(request, sender, sendResponse) {
        this.logger.log(request.type);
        if (request.type === "stat") {
            let resp = {
                enabled: this.isMonitoringEnabled(),
                list: this.videoList
                // list: this.videoList.filter(vid => {
                //     if (!vid.tabId) {
                //         return true;
                //     }
                //     return (vid.tabId == this.activeTabId);
                // })
            };
            sendResponse(resp);
        }
        else if (request.type === "cmd") {
            this.userDisabled = request.enabled === false;
            this.logger.log("request.enabled:" + request.enabled);
            if (request.enabled && !this.connector.isConnected()) {
                this.connector.launchApp();
                return;
            }
            this.updateActionIcon();
        }
        else if (request.type === "vid") {
            let vid = request.itemId;
            this.connector.postMessage("/vid", {
                vid: vid + "",
            });
        }
        else if (request.type === "clear") {
            this.connector.postMessage("/clear", {});
        }
        else if (request.type === "xdm-blob-download-confirmed") {
            // User approved oversized blob download from confirm.html
            this.captureAndStreamBlob(request.blobUrl, request.filename, request.mime, request.tabId);
        }
        else if (request.type === "xdm-blob-download-intent") {
            // Content script detected a blob download action (click/programmatic)
            this.logger.log("Blob download intent: " + request.blobUrl);
            const tabId = sender.tab?.id || null;
            this.captureAndStreamBlob(request.blobUrl, request.filename, request.mime, tabId);
        }
    }

    sendLinkToXDM(info, tab) {
        let url = info.linkUrl;
        if (!this.isSupportedProtocol(url)) {
            url = info.srcUrl;
        }
        if (!this.isSupportedProtocol(url)) {
            url = info.pageUrl;
        }
        if (!this.isSupportedProtocol(url)) {
            return;
        }
        this.triggerDownload(url, null, info.pageUrl, null, null);
    }

    sendImageToXDM(info, tab) {
        let url = info.srcUrl;
        if (!this.isSupportedProtocol(url))
            url = info.linkUrl;
        if (!this.isSupportedProtocol(url)) {
            url = info.pageUrl;
        }
        if (!this.isSupportedProtocol(url)) {
            return;
        }
        this.triggerDownload(url, null, info.pageUrl, null, null);
    }

    sendBlobMediaToXDM(info, tab) {
        // Try srcUrl (blob), then linkUrl, then pageUrl — capture any blob URL
        let url = info.srcUrl;
        if (!this.isBlobUrl(url)) url = info.linkUrl;
        if (!this.isBlobUrl(url)) {
            // No blob URL found in context; fall through to normal http path
            this.sendImageToXDM(info, tab);
            return;
        }
        const filename = info.menuItemId ? undefined : (tab?.title || undefined);
        this.startBlobTransfer(url, filename || this.deriveBlobFilename(url, ""), undefined, tab?.id);
    }

    onMenuClicked(info, tab) {
        if (info.menuItemId == "download-any-link") {
            this.sendLinkToXDM(info, tab);
        }
        if (info.menuItemId == "download-image-link") {
            this.sendImageToXDM(info, tab);
        }
        if (info.menuItemId == "download-blob-media") {
            this.sendBlobMediaToXDM(info, tab);
        }
    }

    attachContextMenu() {
        chrome.contextMenus.create({
            id: 'download-any-link',
            title: "Download with XDM",
            contexts: ["link", "video", "audio", "all"]
        });

        chrome.contextMenus.create({
            id: 'download-image-link',
            title: "Download Image with XDM",
            contexts: ["image"]
        });

        chrome.contextMenus.create({
            id: 'download-blob-media',
            title: "Download Blob Media with XDM",
            contexts: ["video", "audio", "image", "link"]
        });

        chrome.contextMenus.onClicked.addListener(this.onMenuClicked.bind(this));
    }

    onTabActivated(activeInfo) {
        this.activeTabId = activeInfo.tabId + "";
        this.logger.log("Active tab: " + this.activeTabId);
        this.updateActionIcon();
    }
}
