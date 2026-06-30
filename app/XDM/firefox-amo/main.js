// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// MV3 service worker entry (classic scope; uses importScripts so the existing
// class files — logger/connector/request-watcher/app — work without ES-module rewrite).
"use strict";

importScripts(
    "app/logger.js",
    "app/connector.js",
    "app/request-watcher.js",
    "app/app.js"
);

const app = new App();
app.start();
