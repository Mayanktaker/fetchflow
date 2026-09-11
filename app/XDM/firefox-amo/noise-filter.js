// © Mayanktaker Computers & Web Development | https://mayanktaker.com
"use strict";
// Shared junk-URL filter (classic-script twin of chrome-extension/noise-filter.js;
// core mirrors the list in NetworkHelper.cs). Loaded first via manifest scripts.
const NOISE_URL_SUBSTRINGS = [
    "complete/search", "/complete/s",
    "google.com/async/", "google.com/httpservice/",
    "getdatasyncids", "sw.js", "/api/timedtext",
    "generate_204", "gen_204",
    "/api/stats",
    "play.google.com/log", "google.com/log",
    "safebrowsing",
    "fbsbx.com",
    "_next/image",
];

// True for autocomplete/telemetry/SW/sticker-CDN noise that must never capture
function isNoiseUrl(url) {
    if (!url) return false;
    try {
        const low = ("" + url).toLowerCase();
        return NOISE_URL_SUBSTRINGS.some(s => low.indexOf(s) >= 0);
    } catch { return false; }
}

// File hosts hide the name in query params (?file=game.rar) — match only at a
// query-param value boundary, never as a raw URL substring
function matchesFileExtInUrl(url, file, fileExts) {
    try {
        const full = (url + " " + (file || "")).toUpperCase();
        if ((fileExts || []).some(ext => full.indexOf("." + ext + ".") >= 0)) {
            return true;
        }
        const u = new URL(url);
        for (const [, value] of u.searchParams) {
            const v = ("" + value).toUpperCase();
            if ((fileExts || []).some(ext => v.endsWith("." + ext))) {
                return true;
            }
        }
    } catch { }
    return false;
}
