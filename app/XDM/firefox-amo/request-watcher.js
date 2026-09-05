// © Mayanktaker Computers & Web Development | https://mayanktaker.com
"use strict";

class RequestWatcher {
    constructor(callback, statusCallback) {
        this.logger = new Logger();
        this.blockedHosts = [];
        this.mediaExts = [];
        this.fileExts = [];
        this.requestMap = new Map();
        this.callback = callback;
        this.matchingHosts = [];
        this.mediaTypes = [];
        this.onSendHeadersEventCallback = this.onSendHeadersEvent.bind(this);
        this.onHeadersReceivedEventCallback = this.onHeadersReceivedEvent.bind(this);
        this.onErrorOccurredEventCallback = this.onErrorOccurredEvent.bind(this);
        this.urlPatterns = [];
        this.requestFileExts = [];
        this.statusCallback = statusCallback;
    }

    updateConfig(config) {
        if (config.blockedHosts) {
            this.blockedHosts = config.blockedHosts
        }
        if (config.fileExts) {
            this.fileExts = config.fileExts
        }
        if (config.mediaExts) {
            this.mediaExts = config.mediaExts
        }
        if (config.matchingHosts) {
            this.matchingHosts = config.matchingHosts
        }
        if (config.mediaTypes) {
            this.mediaTypes = config.mediaTypes
        }
        if (config.requestFileExts) {
            this.requestFileExts = config.requestFileExts
        }
        if (config.urlPatterns) {
            this.urlPatterns = config.urlPatterns.map(pattern => {
                try {
                    return new RegExp(pattern, "i");
                } catch { }
            }).filter(item => item || false);
        }
    }

    isMatchingRequest(res) {
        if (this.isInValidResourceType(res) || this.isInValidStatus(res)) {
            return false;
        }

        let u = new URL(res.url);

        let hostName = u.host;
        if (this.blockedHosts.find(h => hostName.indexOf(h) >= 0)) {
            return false;
        }

        let path = u.pathname;
        let upath = path.toUpperCase();
        if (this.mediaExts.find(e => upath.endsWith("." + e))) {
            return true;
        }

        if (this.requestFileExts.find(e => upath.endsWith("." + e))) {
            return true;
        }

        try {
            if (this.urlPatterns.find(re => re.test(res.url))) {
                return true;
            }
        } catch { }

        const responseHeaders = res.responseHeaders || [];
        let mediaType = responseHeaders.find(h => h["name"].toUpperCase() === "CONTENT-TYPE");
        if (mediaType && this.mediaTypes.find(m => mediaType["value"].indexOf(m) >= 0)) {
            return true;
        }

        if (this.fileExts.find(e => upath.endsWith("." + e))) {
            return true;
        }

        // Query-aware fallback for short file-host links (e.g. bzzhr.to/xxxx).
        try {
            const ufull = res.url.toUpperCase();
            if (this.fileExts.find(e => ufull.indexOf("." + e) >= 0)) {
                return true;
            }
        } catch { }

        let contentDisposition = responseHeaders.find(h => h["name"].toUpperCase() === "CONTENT-DISPOSITION");
        if (contentDisposition && this.fileExts.find(ext => contentDisposition["value"].toUpperCase().indexOf("." + ext) >= 0)) {
            return true;
        }

        if (this.matchingHosts.find(h => hostName.indexOf(h) >= 0)) {
            return true;
        }
    }

    onSendHeadersEvent(info) {
        // Track GET/POST/HEAD so file-host POST downloads (e.g. bzzhr.to) are not
        // dropped before response-header matching; other verbs still need matchingHost.
        const method = (info.method || "GET").toUpperCase();
        if ((method === "GET" || method === "POST" || method === "HEAD")
            || (this.matchingHosts
                && this.matchingHosts.find(matchingHost => info.url.indexOf(matchingHost) > 0))) {
            this.requestMap.set(info.requestId, info);
        }
    }

    // Attachment fast-path: server explicitly forces a save dialog
    // (Content-Disposition: attachment; filename="...rar"). Returns the filename
    // when its extension is a known download type, else null. Inline responses
    // (video/audio streaming, pages) never match — playback stays untouched.
    getAttachmentFilename(res) {
        try {
            const headers = res.responseHeaders || [];
            const cd = headers.find(h => h["name"].toUpperCase() === "CONTENT-DISPOSITION");
            if (!cd || !cd["value"] || cd["value"].toLowerCase().indexOf("attachment") < 0) return null;
            const v = cd["value"];
            let m = v.match(/filename\*\s*=\s*UTF-8''([^;\s]+)/i) || v.match(/filename\s*=\s*"([^"]+)"/i) || v.match(/filename\s*=\s*([^;\s]+)/i);
            if (!m) return null;
            let name = decodeURIComponent(m[1] || m[2] || m[3] || "").trim();
            if (!name) return null;
            if (this.fileExts.find(e => name.toUpperCase().endsWith("." + e))) return name;
            return null;
        } catch { return null; }
    }

    onHeadersReceivedEvent(res) {
        let reqId = res.requestId;
        let req = this.requestMap.get(reqId);
        if (req) {
            this.requestMap.delete(reqId);
            if (res.url.indexOf("127.0.0.1") >= 0) {
                return;
            }
            // Attachment fast-path: take over the response here and cancel the
            // browser request, so Firefox never creates a download item and its
            // save box never opens. downloads.onCreated stays as backup; native
            // dedup drops the double if both fire.
            if (this.statusCallback() && !this.isInValidResourceType(res) && !this.isInValidStatus(res)) {
                const attachFile = this.getAttachmentFilename(res);
                if (attachFile) {
                    try {
                        let data = this.createRequestData(req, res, attachFile, null, req.tabId);
                        data.download = true;
                        if (this.callback) this.callback(data);
                    } catch (e) { }
                    return { cancel: true };
                }
            }
            // Anything else (media, inline images, pages) goes to /media only —
            // inline images must never open download dialogs (downloads.onCreated
            // + attachment fast-path own real file captures).
            if (this.callback && this.isMatchingRequest(res) && this.statusCallback()) {
                if (req.tabId !== -1) {
                    chrome.tabs.get(
                        req.tabId,
                        tab => {
                            if (chrome.runtime.lastError) {
                                this.postMedia(req, res, null);
                                return;
                            }
                            this.postMedia(req, res, tab);
                        }
                    );
                } else {
                    this.postMedia(req, res, null);
                }
            }
        }
    }

    postMedia(req, res, tab) {
        let file = tab ? tab.title : null;
        let tabUrl = tab ? tab.url : null;
        this.callback(this.createRequestData(req, res, file, tabUrl, req.tabId));
    }

    onErrorOccurredEvent(info) {
        let reqId = info.requestId;
        this.requestMap.delete(reqId);
    }

    register() {
        chrome.webRequest.onSendHeaders.addListener(
            this.onSendHeadersEventCallback,
            { urls: ["http://*/*", "https://*/*"] },
            ["requestHeaders"]
        );

        // "blocking" enables the attachment fast-path cancel (Firefox MV3 still
        // allows blocking webRequest; manifest holds webRequestBlocking).
        chrome.webRequest.onHeadersReceived.addListener(
            this.onHeadersReceivedEventCallback,
            { urls: ["http://*/*", "https://*/*"] },
            ["blocking", "responseHeaders"]
        );

        chrome.webRequest.onErrorOccurred.addListener(
            this.onErrorOccurredEventCallback,
            { urls: ["http://*/*", "https://*/*"] }
        );
    }

    unRegister() {
        chrome.webRequest.onSendHeaders.removeListener(this.onSendHeadersEventCallback);
        chrome.webRequest.onHeadersReceived.removeListener(this.onHeadersReceivedEventCallback);
        chrome.webRequest.onErrorOccurred.removeListener(this.onErrorOccurredEventCallback);
    }

    createRequestData(req, res, title, tabUrl, tabId) {
        var data = {
            url: res.url,
            file: title,
            requestHeaders: {},
            responseHeaders: {},
            cookies: "",
            method: req.method,
            userAgent: navigator.userAgent,
            tabUrl: tabUrl,
            tabId: tabId + "",
            download: false
        };

        let cookies = [];

        if (req.extraHeaders) {
            req.extraHeaders.forEach(h => {
                if (h.name.toLowerCase() === 'cookie') {
                    cookies.push(h.value);
                }
                this.addToDict(data.requestHeaders, h.name, h.value);
            });
        }
        if (req.requestHeaders) {
            req.requestHeaders.forEach(h => {
                if (h.name.toLowerCase() === 'cookie') {
                    cookies.push(h.value);
                }
                this.addToDict(data.requestHeaders, h.name, h.value);
            });
        }
        if (res.responseHeaders) {
            res.responseHeaders.forEach(h => {
                this.addToDict(data.responseHeaders, h.name, h.value);
            });
        }
        if (cookies.length > 0) {
            data.cookie = cookies.join("; ");
            data.cookies = data.cookie;
        }
        return data;
    }

    addToDict(dict, key, value) {
        let values = dict[key];
        if (values) {
            values.push(value);
        } else {
            dict[key] = [value];
        }
    }

    isInValidStatus(res) {
        return res.statusCode && res.statusCode !== 200 && res.statusCode !== 206;
    }

    isInValidResourceType(res) {
        return res.type && (res.type === "stylesheet" || res.type === "script" || res.type === "font" || res.type === "websocket");
    }
}