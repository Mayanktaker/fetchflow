// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Parity gate for the junk-URL blocklist: chrome module, firefox classic script
// and core NetworkHelper.cs must carry the identical substring list, and the
// reported/live-DB junk URLs must be rejected while legit captures pass.
// Usage: node scripts/test-noise-filter.mjs (wired into build_all.sh gate).
import { readFileSync } from "node:fs";
import vm from "node:vm";
import { isNoiseUrl as chromeIsNoise, matchesFileExtInUrl, NOISE_URL_SUBSTRINGS as chromeList } from "../app/XDM/chrome-extension/noise-filter.js";

const root = new URL("..", import.meta.url).pathname;
let failures = 0;
const check = (cond, label) => {
    console.log((cond ? "  [PASS] " : "  [FAIL] ") + label);
    if (!cond) failures++;
};

// Firefox twin is a classic script (no exports) — evaluate and lift bindings
const ffCode = readFileSync(root + "app/XDM/firefox-amo/noise-filter.js", "utf8")
    + "\n;globalThis.__list = NOISE_URL_SUBSTRINGS; globalThis.__isNoise = isNoiseUrl; globalThis.__match = matchesFileExtInUrl;";
// Fresh vm contexts lack web globals — provide what a background page has
const ffSandbox = { URL, URLSearchParams, decodeURIComponent };
vm.createContext(ffSandbox);
vm.runInContext(ffCode, ffSandbox);

// Core twin: extract the string literals from the NoiseUrlSubstrings block
const csSrc = readFileSync(root + "app/XDM/XDM.Core/BrowserMonitoring/NetworkHelper.cs", "utf8");
const csBlock = csSrc.slice(csSrc.indexOf("NoiseUrlSubstrings = new[]"), csSrc.indexOf("};", csSrc.indexOf("NoiseUrlSubstrings")));
const csList = [...csBlock.matchAll(/"([^"]+)"/g)].map((m) => m[1]);

// List parity across all three sites (order-insensitive)
const sameSet = (a, b) => a.length === b.length && a.every((s) => b.includes(s));
check(sameSet(chromeList, ffSandbox.__list), `chrome/firefox lists match (${chromeList.length} entries)`);
check(sameSet(chromeList, csList), "chrome/core lists match");
check(sameSet(ffSandbox.__list, csList), "firefox/core lists match");

// Manifest must load the classic twin before its consumers
const manifest = JSON.parse(readFileSync(root + "app/XDM/firefox-amo/manifest.json", "utf8"));
check(manifest.background.scripts[0] === "noise-filter.js", "firefox manifest loads noise-filter.js first");

// Reported + live-DB junk URLs rejected by BOTH extension twins
const junk = [
    "https://cdn.fbsbx.com/v/t59.2708-21/535444823_1992905721527548_4606810233348303977_n.gif?x=1",
    "https://www.google.com/complete/search?q=Hancock&cp=0&client=gws-wiz-serp",
    "https://www.google.com/complete/s?q&cp=0&client=gws-wiz-serp",
    "https://www.google.com/async/bgasy?ei=x&client=firefox-b-d&async=_fmt:jspb",
    "https://www.google.com/httpservice/retry/ValidationAsyncService/Validate?a=1",
    "https://www.youtube.com/getDatasyncIdsEndpoint",
    "https://www.youtube.com/sw.js_data",
    "https://www.youtube.com/api/timedtext?v=abc&caps=asr",
    "https://suggestqueries-clients6.youtube.com/complete/search?client=youtube&q=test",
    "https://devin.ai/_next/image?url=%2Fassets%2Fimages%2Fhome-hero%2Fhero_new.webp&w=3840&q=75",
];
for (const u of junk) {
    check(chromeIsNoise(u) && ffSandbox.__isNoise(u), `junk rejected: ${u.slice(0, 60)}…`);
}

// Legit captures must pass, incl. file-host query names and no sw.js false-hit
const legit = [
    "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
    "https://rr1---sn.googlevideo.com/videoplayback?itag=22",
    "https://bzzhr.to/wjwse1a5544o",
    "https://cdn.example.com/video.mp4",
];
for (const u of legit) {
    check(!chromeIsNoise(u) && !ffSandbox.__isNoise(u), `legit passes: ${u.slice(0, 60)}…`);
}
check(matchesFileExtInUrl("https://host/dl?file=game.rar&token=1", null, ["MP4", "RAR"]), "file-host ?file=game.rar matches");
check(ffSandbox.__match("https://host/dl?file=game.rar&token=1", null, ["MP4", "RAR"]), "firefox twin ?file=game.rar matches");
check(!matchesFileExtInUrl("https://www.youtube.com/sw.js_data", null, ["JS", "TXT", "GIF"]), "sw.js_data is not an ext hit");

console.log(failures === 0 ? "NOISE-FILTER-PARITY-OK" : `NOISE-FILTER-PARITY-FAILED (${failures})`);
process.exit(failures === 0 ? 0 : 1);
