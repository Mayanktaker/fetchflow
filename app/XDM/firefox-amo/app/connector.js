// © Mayanktaker Computers & Web Development | https://mayanktaker.com
"use strict";

// Phase2.3: probe a small port range so the extension survives if 8597 is taken
const APP_BASE_PORTS = [8597, 8598, 8599, 8600, 8601, 8602, 8603];
let APP_BASE_URL = "http://127.0.0.1:" + APP_BASE_PORTS[0];

class Connector {
    constructor(onMessage, onDisconnect) {
        this.logger = new Logger();
        this.onMessage = onMessage;
        this.onDisconnect = onDisconnect;
        this.connected = undefined;
        this.portIndex = 0;
    }

    connect() {
        // This will be replaced with websocket in future versions
        setInterval(this.onTimer.bind(this), 5000);
    }

    onTimer() {
        fetch(APP_BASE_URL + "/sync")
            .then(this.onResponse.bind(this))
            .catch(err => {
                // Phase2.3: rotate to the next candidate port while disconnected
                if (!this.connected) {
                    this.portIndex = (this.portIndex + 1) % APP_BASE_PORTS.length;
                    APP_BASE_URL = "http://127.0.0.1:" + APP_BASE_PORTS[this.portIndex];
                }
                this.disconnect();
            });
    }

    disconnect() {
        this.connected = false;
        this.onDisconnect();
    }

    isConnected() {
        return this.connected;
    }

    onResponse(res) {
        this.connected = true;
        res.json().then(json => this.onMessage(json)).catch(err => this.disconnect());
    }

    postMessage(url, data) {
        fetch(APP_BASE_URL + url, { method: "POST", body: JSON.stringify(data) })
            .then(this.onResponse.bind(this))
            .catch(err => this.disconnect());
    }

    // Wayland/Phase2.4: open the OS-registered xdm-app:// scheme so the desktop launches XDM
    launchApp() {
        try {
            chrome.tabs.create({ url: "xdm-app://launch" });
        } catch (e) {
            this.logger.log("launchApp failed: " + e);
        }
    }
}
