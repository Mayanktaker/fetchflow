// © Mayanktaker Computers & Web Development | https://mayanktaker.com
"use strict";
import Logger from './logger.js';
import { isNoiseUrl, matchesFileExtInUrl } from './noise-filter.js';

export default class RequestWatcher {
    constructor(callback) {
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

    isInValidStatus(res) {
        return res.statusCode && res.statusCode !== 200 && res.statusCode !== 206;
    }

    isInValidResourceType(res) {
        return res.type && (
            res.type === "stylesheet" ||
            res.type === "script" ||
            res.type === "font" ||
            res.type === "websocket" ||
            res.type === "image" ||
            res.type === "imageset" ||
            res.type === "ping" ||
            res.type === "beacon" ||
            res.type === "csp_report"
        );
    }

    isMatchingRequest(res) {
        if (this.isInValidResourceType(res) || this.isInValidStatus(res)) {
            return false;
        }
        if (isNoiseUrl(res.url)) {
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
        if (mediaType && ("" + mediaType["value"]).toLowerCase().startsWith("image/")) {
            return false;
        }
        if (mediaType && this.mediaTypes.find(m => mediaType["value"].indexOf(m) >= 0)) {
            return true;
        }

        // Query-aware fallback for media stream links
        if (matchesFileExtInUrl(res.url, null, this.mediaExts)) {
            return true;
        }

        // matchingHosts (youtube/googlevideo) must NOT match every API call on the
        // host (getDatasyncIdsEndpoint, sw.js_data, stats). Require a media signal:
        // segmented-playback URL or an audio/video/octet-stream response.
        if (this.matchingHosts.find(h => hostName.indexOf(h) >= 0)) {
            const low = res.url.toLowerCase();
            if (low.indexOf("videoplayback") >= 0 || low.indexOf(".m3u8") >= 0 || low.indexOf(".mpd") >= 0) {
                return true;
            }
            const ct = (mediaType && mediaType["value"] ? mediaType["value"] : "").toLowerCase();
            if (ct.indexOf("audio/") >= 0 || ct.indexOf("video/") >= 0 || ct.indexOf("application/octet") >= 0) {
                return true;
            }
            return false;
        }
    }

    onSendHeadersEvent(info) {
        // File hosts (e.g. bzzhr.to / buzzheavier) issue downloads via POST forms;
        // dropping non-GET here silently misses those captures. Track GET/POST/HEAD
        // and let isMatchingRequest decide on response headers; other verbs still
        // require a matchingHost (YouTube-style segmented fetches).
        const method = (info.method || "GET").toUpperCase();
        if ((method === "GET" || method === "POST" || method === "HEAD")
            || (this.matchingHosts
                && this.matchingHosts.find(matchingHost => info.url.indexOf(matchingHost) > 0))) {
            this.requestMap.set(info.requestId, info);
            return;
        }
    }

    onHeadersReceivedEvent(res) {
        let reqId = res.requestId;
        let req = this.requestMap.get(reqId);
        if (req) {
            this.requestMap.delete(reqId);
            if (res.url && (res.url.indexOf("127.0.0.1") >= 0 || isNoiseUrl(res.url))) {
                return;
            }
            if (this.callback && this.isMatchingRequest(res)) {
                if (req.tabId !== -1) {
                    chrome.tabs.get(
                        req.tabId,
                        tab => {
                            this.callback(this.createRequestData(req, res, tab.title, tab.url, req.tabId));
                        }
                    );
                } else {
                    this.callback(this.createRequestData(req, res, null, null, req.tabId));
                }
            }
        }
    }

    onErrorOccurredEvent(info) {
        let reqId = info.requestId;
        this.requestMap.delete(reqId);
    }

    register() {
        chrome.webRequest.onSendHeaders.addListener(
            this.onSendHeadersEventCallback,
            { urls: ["http://*/*", "https://*/*"] },
            ["extraHeaders", "requestHeaders"]
        );

        chrome.webRequest.onHeadersReceived.addListener(
            this.onHeadersReceivedEventCallback,
            { urls: ["http://*/*", "https://*/*"] },
            ["extraHeaders", "responseHeaders"]
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
        let data = {
            url: res.url,
            file: title,
            requestHeaders: {},
            responseHeaders: {},
            cookie: undefined,
            method: req.method,
            userAgent: navigator.userAgent,
            tabUrl: tabUrl,
            tabId: tabId + ""
        };

        let cookies = [];

        if (req.extraHeaders) {
            req.extraHeaders.forEach(h => {
                if (h.name === 'Cookie' || h.name === 'cookie') {
                    cookies.push(h.value);
                }
                this.addToDict(data.requestHeaders, h.name, h.value);
            });
        }
        if (req.requestHeaders) {
            req.requestHeaders.forEach(h => {
                if (h.name === 'Cookie' || h.name === 'cookie') {
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
}
