// © Mayanktaker Computers & Web Development | https://mayanktaker.com
"use strict";

class App {

    constructor() {
        this.logger = new Logger();
        this.videoList = [];
        this.blockedHosts = [];
        this.fileExts = [];
        this.requestWatcher = new RequestWatcher(this.onRequestDataReceived.bind(this), this.isMonitoringEnabled.bind(this));
        this.tabsWatcher = [];
        this.userDisabled = false;
        this.appEnabled = false;
        this.blobMaxBytes = 256 * 1024 * 1024; // 256 MiB default cap
        this.onTabUpdateCallback = this.onTabUpdate.bind(this);
        this.onDownloadCreatedCallback = this.onDownloadCreated.bind(this);
        this.activeTabId = -1;
        this.connector = new Connector(this.onMessage.bind(this), this.onDisconnect.bind(this));
        this.pendingDownloads = [];
        this.handledDownloadIds = new Set();
        this.pendingDownloadsMap = new Map();
        this.configLoaded = false;       // true once the app's first /sync arrives this wake
        this.pendingDownloadEvents = []; // downloads.onCreated seen before config arrived
    }

    start() {
        this.logger.log("starting...");
        // Query the active tab immediately so activeTabId is known on startup
        chrome.tabs.query({ active: true, currentWindow: true }, tabs => {
            if (tabs && tabs[0]) {
                this.activeTabId = tabs[0].id + "";
            }
        });
        // Load cached config to prevent missing requests on wakeup
        chrome.storage.local.get(["fetchflowConfig", "xdmConfig"], (res) => {
            const cfg = res.fetchflowConfig || res.xdmConfig;
            if (cfg) {
                this.updateConfig(cfg);
            }
        });
        this.starAppConnector();
        this.register();
        // Firefox MV3 event pages idle out after ~30s; an open WebSocket or a plain
        // setInterval does NOT reset that timer. A periodic extension-API call does
        // (documented Firefox workaround) — it keeps the background alive so the
        // WebSocket stays connected and downloads are intercepted in real time.
        this.keepaliveInterval = setInterval(() => {
            try {
                browser.runtime.getPlatformInfo();
            } catch (e) { }
        }, 20000);
        this.logger.log("started.");
    }

    starAppConnector() {
        this.connector.connect();
    }

    onMessage(msg) {
        this.logger.log("message from FetchFlow");
        this.logger.log(msg);
        chrome.storage.local.set({ "fetchflowConfig": msg, "xdmConfig": msg });
        this.updateConfig(msg);
        this.flushPendingDownloads();
    }

    // Send downloads that were queued while FetchFlow was offline (called once the relay reconnects)
    flushPendingDownloads() {
        // Replay browser download events captured before this wake's config arrived —
        // they must be judged with real fileExts/enabled flags, not the empty defaults.
        if (this.pendingDownloadEvents.length > 0) {
            const queued = this.pendingDownloadEvents;
            this.pendingDownloadEvents = [];
            queued.forEach(d => this.onDownloadCreated(d));
        }
        if (!this.connector.isConnected() || !this.pendingDownloads || this.pendingDownloads.length === 0) {
            return;
        }
        const queued = this.pendingDownloads;
        this.pendingDownloads = [];
        queued.forEach(data => this.connector.postMessage("/download", data));
    }

    updateConfig(msg) {
        this.appEnabled = msg.enabled === true;
        this.configLoaded = true;
        this.fileExts = msg.fileExts || [];
        this.blockedHosts = msg.blockedHosts || [];
        this.tabsWatcher = msg.tabsWatcher || [];
        this.videoList = msg.videoList || [];
        if (msg.blobMaxBytes && msg.blobMaxBytes > 0) {
            this.blobMaxBytes = msg.blobMaxBytes;
        }
        this.requestWatcher.updateConfig({
            blockedHosts: msg.blockedHosts || [],
            fileExts: msg.fileExts || [],
            mediaExts: msg.requestFileExts || [],
            matchingHosts: msg.matchingHosts || [],
            mediaTypes: msg.mediaTypes || []
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
        if (this.isMonitoringEnabled() && this.connector.isConnected()) {
            if (data.download) {
                this.connector.postMessage("/download", data);
            } else {
                this.connector.postMessage("/media", data);
            }
        }
    }

    // Firefox download interception via the downloads API.
    // Listens to onCreated for immediate matches, and onChanged for Content-Disposition names.
    onDownloadCreated(download) {
        this.logger.log("onDownloadCreated");
        this.logger.log(download);
        if (!this.isMonitoringEnabled()) {
            // Cold-wake race: downloads.onCreated can fire before this wake's first
            // /sync arrives (config unknown, fileExts empty). Queue and re-judge
            // once config lands; only a real disabled flag drops the event.
            if (!this.configLoaded) {
                this.pendingDownloadEvents.push(download);
            }
            return;
        }
        let url = download.url || "";
        if (this.isBlobUrl(url) || !this.isSupportedProtocol(url)) {
            return;
        }
        if (url.indexOf("127.0.0.1") >= 0) {
            return;
        }
        if (this.shouldTakeOver(url, download.filename)) {
            this.interceptDownload(download, download.filename);
        } else {
            this.pendingDownloadsMap.set(download.id, download);
        }
    }

    onDownloadChanged(delta) {
        if (!delta) {
            return;
        }
        if (delta.state && (delta.state.current === "complete" || delta.state.current === "interrupted")) {
            this.handledDownloadIds.delete(delta.id);
            this.pendingDownloadsMap.delete(delta.id);
            this.pendingDownloadEvents = this.pendingDownloadEvents.filter(d => d.id !== delta.id);
            return;
        }
        // Keep queued pre-config events fresh with their resolved filename
        const queued = this.pendingDownloadEvents.find(d => d.id === delta.id);
        if (queued && delta.filename && delta.filename.current) {
            queued.filename = delta.filename.current;
            return;
        }
        if (!this.isMonitoringEnabled()) {
            return;
        }
        if (delta.filename && delta.filename.current) {
            if (this.handledDownloadIds.has(delta.id)) {
                return;
            }
            let item = this.pendingDownloadsMap.get(delta.id);
            let url = item ? item.url : null;
            let filename = delta.filename.current;
            if (url && this.shouldTakeOver(url, filename)) {
                this.interceptDownload(item || { id: delta.id, url: url }, filename);
            }
        }
    }

    interceptDownload(download, filename) {
        if (this.handledDownloadIds.has(download.id)) {
            return;
        }
        this.handledDownloadIds.add(download.id);
        this.pendingDownloadsMap.delete(download.id);
        this.logger.log("Taking over download: " + download.url);
        chrome.downloads.cancel(download.id, () => {
            try {
                chrome.downloads.erase({ id: download.id }, () => { });
            } catch (e) { }
        });
        let referrer = download.referrer;
        if (!referrer && download.finalUrl && download.finalUrl !== download.url) {
            referrer = download.url;
        }
        let size = download.fileSize || download.totalBytes || 0;
        this.triggerDownload(download.url, filename, referrer, size, download.mime);
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
                this.tabsWatcher.some(t => tabUrl.includes(t))) {
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
            this.tabsWatcher.some(t => url.includes(t))) {
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
        chrome.tabs.onUpdated.addListener(
            this.onTabUpdateCallback
        );
        // Firefox download interception via downloads API (cancel + send to app)
        chrome.downloads.onCreated.addListener(
            this.onDownloadCreatedCallback
        );
        chrome.downloads.onChanged.addListener(
            this.onDownloadChanged.bind(this)
        );
        // SPA navigation detection via History API (YouTube, etc.)
        if (chrome.webNavigation && chrome.webNavigation.onHistoryStateUpdated) {
            chrome.webNavigation.onHistoryStateUpdated.addListener(
                this.onHistoryStateUpdated.bind(this)
            );
        }
        chrome.runtime.onMessage.addListener(this.onPopupMessage.bind(this));
        // Firefox MV3 event pages suspend after ~30s idle and downloads.onCreated
        // is NOT a waking event — a sub-minute alarm heartbeat keeps the background
        // alive so download events are never missed (Firefox does not clamp <1min periods).
        chrome.alarms.create("fetchflow-keepalive", { periodInMinutes: 0.4 });
        chrome.alarms.onAlarm.addListener(this.onKeepaliveAlarm.bind(this));
        // request-watcher (webRequest, non-blocking) detects streaming media;
        // file downloads go through downloads.onCreated above.
        // Blob downloads are intercepted at the DOM level by blob-capture.js content script.
        this.requestWatcher.register();
        this.attachContextMenu();
        chrome.tabs.onActivated.addListener(this.onTabActivated.bind(this));
    }

    // Alarm heartbeat: its firing wakes the suspended event page (the whole
    // script re-runs and reconnects); nudge the connector if it is idle.
    onKeepaliveAlarm(alarm) {
        if (alarm.name !== "fetchflow-keepalive") {
            return;
        }
        if (!this.connector.isConnected()) {
            this.connector.connect();
        }
    }

    // MV3/Phase2.1: takeover rule (file-extension based; mirrors chrome-extension/app.js)
    shouldTakeOver(url, file) {
        if (!url) return false;
        let u;
        try { u = new URL(url); } catch { return false; }
        if (u.protocol !== 'http:' && u.protocol !== 'https:') return false;
        let hostName = u.host;
        if (this.blockedHosts && this.blockedHosts.some(item => hostName.includes(item))) {
            return false;
        }
        let cleanFile = file ? file.replace(/\.part$/i, "") : "";
        let path = cleanFile || u.pathname;
        let upath = path.toUpperCase();
        if (cleanFile && !this.fileExts.some(ext => upath.endsWith(ext))) {
            upath = u.pathname.toUpperCase();
        }
        if (this.fileExts && this.fileExts.some(ext => upath.endsWith(ext))) {
            return true;
        }
        return false;
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
            this.logger.log("Cannot transfer blob: not connected to FetchFlow");
            this.connector.launchApp();
            return;
        }
        const size = 0;
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
        browser.tabs.create({ url: "confirm.html?" + params.toString() });
    }

    async captureAndStreamBlob(blobUrl, filename, mime, tabId) {
        this.logger.log("Capturing blob: " + blobUrl);
        const tabIds = tabId ? [parseInt(tabId, 10)] : [];
        if (tabIds.length === 0) {
            const [tab] = await browser.tabs.query({ active: true, currentWindow: true });
            if (tab?.id) tabIds.push(tab.id);
        }
        if (tabIds.length === 0) {
            this.logger.log("No active tab for blob capture");
            return;
        }
        try {
            const response = await browser.tabs.sendMessage(tabIds[0], {
                type: "fetchflow-capture-blob",
                blobUrl,
                filename
            });
            if (response?.error) {
                this.logger.log("Blob capture failed: " + response.error);
                return;
            }
            if (response?.base64) {
                await this.streamBlobToFetchFlow(response.base64, filename, response.mime || mime, response.size || 0, blobUrl);
            }
        } catch (e) {
            this.logger.log("Blob capture message error: " + e.message);
        }
    }

    async streamBlobToFetchFlow(base64Data, filename, mime, size, blobUrl) {
        this.logger.log("=== streamBlobToFetchFlow START ===");
        this.logger.log("Filename: " + filename + " | Size: " + size + " | Base64 len: " + base64Data.length);
        const BLOB_CHUNK_SIZE = 512 * 1024;
        let raw;
        try {
            raw = atob(base64Data);
        } catch (e) {
            this.logger.log("ERROR: base64 decode failed: " + e.message);
            return { error: "base64 decode failed: " + e.message };
        }
        const bytes = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
        this.logger.log("Decoded " + bytes.length + " bytes from base64");

        const totalChunks = Math.max(1, Math.ceil(bytes.length / BLOB_CHUNK_SIZE));
        const transferId = crypto.randomUUID();
        this.logger.log("Transfer ID: " + transferId + " | Total chunks: " + totalChunks);

        // Track active blob transfers for progress badge
        this.activeBlobTransfers = (this.activeBlobTransfers || 0) + 1;

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
                this.logger.log("Posting chunk " + (i + 1) + "/" + totalChunks + " (" + chunk.length + " bytes)");
                const result = await this.connector.postBlobChunk(headers, chunk);
                this.logger.log("Chunk " + (i + 1) + " response: " + JSON.stringify(result));
                if (result && result.error) {
                    this.logger.log("ERROR: Blob chunk error: " + result.error);
                    this.activeBlobTransfers = Math.max(0, (this.activeBlobTransfers || 0) - 1);
                    this.updateActionIcon();
                    return { error: "chunk error: " + result.error };
                }
                // Show progress percentage in badge
                const pct = Math.round(((i + 1) / totalChunks) * 100);
                browser.action.setBadgeText({ text: pct + "%" });
                browser.action.setBadgeBackgroundColor({ color: "#ff6b35" });
            } catch (e) {
                this.logger.log("ERROR: Blob chunk POST failed: " + e.message);
                this.activeBlobTransfers = Math.max(0, (this.activeBlobTransfers || 0) - 1);
                this.updateActionIcon();
                return { error: "POST failed: " + e.message };
            }
        }
        this.logger.log("=== Blob stream COMPLETE: " + transferId + " ===");
        this.activeBlobTransfers = Math.max(0, (this.activeBlobTransfers || 0) - 1);
        this.updateActionIcon();
        return { ok: true, transferId, chunks: totalChunks };
    }

    updateActionIcon() {
        chrome.action.setIcon({ path: this.getActionIcon() });
        let vc = "";
        if (this.videoList && this.videoList.length > 0) {
            vc = this.videoList.length + "";
        }
        chrome.action.setBadgeText({ text: vc });
        if (!this.connector.isConnected()) {
            this.logger.log("Not connected...");
            chrome.action.setPopup({ popup: "error.html" });
            return;
        }
        if (!this.appEnabled) {
            chrome.action.setPopup({ popup: "disabled.html" });
            return;
        }
        else {
            chrome.action.setPopup({ popup: "popup.html" });
            return;
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
        const sendDownload = (cookieStr) => {
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
                cookies: cookieStr,
                requestHeaders: requestHeaders,
                responseHeaders: responseHeaders,
                file: file,
                filename: file,
                fileSize: size,
                mimeType: mime
            };
            this.logger.log(data);
            if (this.connector.isConnected()) {
                this.connector.postMessage("/download", data);
            } else {
                // FetchFlow isn't reachable — launch it and hold the download until it connects
                this.pendingDownloads.push(data);
                this.connector.launchApp();
            }
        };
        try {
            chrome.cookies.getAll({ "url": url }, cookies => {
                if (chrome.runtime.lastError) {
                    sendDownload(undefined);
                    return;
                }
                let cookieStr = cookies && cookies.length > 0
                    ? cookies.map(cookie => cookie.name + "=" + cookie.value).join("; ")
                    : undefined;
                sendDownload(cookieStr);
            });
        } catch (e) {
            // cookies API can be unavailable without permission; cookies are optional for downloads
            this.logger.log("cookies API unavailable: " + e);
            sendDownload(undefined);
        }
    }
    diconnect() {
        this.onDisconnect();
    }

    onPopupMessage(request, sender, sendResponse) {
        this.logger.log(request.type);
        if (request.type === "stat") {
            let list = (this.videoList || []).slice().sort((a, b) => {
                let aMatches = (a.tabId && a.tabId == this.activeTabId) ? 1 : 0;
                let bMatches = (b.tabId && b.tabId == this.activeTabId) ? 1 : 0;
                return bMatches - aMatches;
            });
            let resp = {
                enabled: this.isMonitoringEnabled(),
                list: list,
                health: this.connector.getHealthInfo()
            };
            sendResponse(resp);
        }
        else if (request.type === "ping") {
            this.connector.pingNow();
            sendResponse({ health: this.connector.getHealthInfo() });
        }
        else if (request.type === "reconnect") {
            this.connector.reconnectNow();
            sendResponse({ health: this.connector.getHealthInfo() });
        }
        else if (request.type === "capture-test") {
            // End-to-end diagnostics: start a REAL browser download of a tiny file.
            // If interception works, this download is cancelled and FetchFlow takes
            // it over — exercising downloads.onCreated → shouldTakeOver → /download.
            if (!this.isMonitoringEnabled()) {
                sendResponse({ error: "Monitoring is disabled — enable it first" });
                return;
            }
            if (!this.connector.isConnected()) {
                sendResponse({ error: "FetchFlow app is not connected — open it first" });
                return;
            }
            chrome.downloads.download(
                { url: "https://speed.hetzner.de/100KB.bin", saveAs: false },
                (downloadId) => {
                    if (chrome.runtime.lastError || downloadId === undefined) {
                        sendResponse({ error: "Browser refused the test download: " + (chrome.runtime.lastError?.message || "unknown") });
                        return;
                    }
                    sendResponse({ ok: true });
                }
            );
            return true; // async sendResponse
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
        else if (request.type === "fetchflow-blob-download-confirmed" || request.type === "xdm-blob-download-confirmed") {
            this.captureAndStreamBlob(request.blobUrl, request.filename, request.mime, request.tabId);
        }
        else if (request.type === "fetchflow-blob-download-data" || request.type === "xdm-blob-download-data") {
            // Page context captured blob data at intent time — stream directly
            this.logger.log("=== Blob download data received ===");
            this.logger.log("Filename: " + request.filename);
            this.logger.log("Size: " + request.size);
            this.logger.log("Base64 length: " + (request.base64 ? request.base64.length : "NONE"));
            this.logger.log("Mime: " + request.mime);
            this.logger.log("Connector connected: " + this.connector.isConnected());
            const tabId = sender.tab?.id || null;
            if (!this.connector.isConnected()) {
                this.logger.log("ERROR: Cannot stream blob — FetchFlow is not connected!");
                sendResponse({ error: "FetchFlow not connected" });
                return;
            }
            if (request.base64) {
                sendResponse({ ok: true, msg: "streaming started" });
                // Stream and forward result to content script for page-console visibility
                this.streamBlobToFetchFlow(request.base64, request.filename, request.mime, request.size, request.blobUrl)
                    .then(result => {
                        if (tabId) {
                            browser.tabs.sendMessage(tabId, {
                                type: "fetchflow-stream-result",
                                success: !result?.error,
                                error: result?.error || null,
                                detail: result
                            });
                        }
                    });
            } else {
                this.logger.log("ERROR: No base64 data in blob-download-data message!");
                sendResponse({ error: "No base64 data" });
            }
        }
        else if (request.type === "fetchflow-blob-download-intent" || request.type === "xdm-blob-download-intent") {
            // Content script detected a blob download action (click/programmatic)
            this.logger.log("Blob download intent: " + request.blobUrl);
            const tabId = sender.tab?.id || null;
            this.captureAndStreamBlob(request.blobUrl, request.filename, request.mime, tabId);
        }
    }

    sendLinkToFetchFlow(info, tab) {
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

    sendImageToFetchFlow(info, tab) {
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

    sendBlobMediaToFetchFlow(info, tab) {
        let url = info.srcUrl;
        if (!this.isBlobUrl(url)) url = info.linkUrl;
        if (!this.isBlobUrl(url)) {
            this.sendImageToFetchFlow(info, tab);
            return;
        }
        const filename = info.menuItemId ? undefined : (tab?.title || undefined);
        this.startBlobTransfer(url, filename || this.deriveBlobFilename(url, ""), undefined, tab?.id);
    }

    onMenuClicked(info, tab) {
        if (info.menuItemId == "download-any-link") {
            this.sendLinkToFetchFlow(info, tab);
        }
        if (info.menuItemId == "download-image-link") {
            this.sendImageToFetchFlow(info, tab);
        }
        if (info.menuItemId == "download-blob-media") {
            this.sendBlobMediaToFetchFlow(info, tab);
        }
    }

    attachContextMenu() {
        browser.menus.create({
            id: 'download-any-link',
            title: "Download with FetchFlow",
            contexts: ["link", "video", "audio", "all"]
        });

        browser.menus.create({
            id: 'download-image-link',
            title: "Download Image with FetchFlow",
            contexts: ["image"]
        });

        browser.menus.create({
            id: 'download-blob-media',
            title: "Download Blob Media with FetchFlow",
            contexts: ["video", "audio", "image", "link"]
        });

        browser.menus.onClicked.addListener(this.onMenuClicked.bind(this));
    }

    onTabActivated(activeInfo) {
        this.activeTabId = activeInfo.tabId + "";
        this.logger.log("Active tab: " + this.activeTabId);
        this.updateActionIcon();
    }
}
