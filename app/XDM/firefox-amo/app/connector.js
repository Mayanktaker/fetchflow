// © Mayanktaker Computers & Web Development | https://mayanktaker.com
"use strict";

// Phase6: probe a small port range; try WebSocket first, fall back to HTTP polling.
const APP_BASE_PORTS = [8597, 8598, 8599, 8600, 8601, 8602, 8603];
let currentPortIndex = 0;
let httpBaseUrl = "http://127.0.0.1:" + APP_BASE_PORTS[currentPortIndex];

class Connector {
    constructor(onMessage, onDisconnect) {
        this.logger = new Logger();
        this.onMessage = onMessage;
        this.onDisconnect = onDisconnect;
        this.connected = undefined;
        this.portIndex = 0;
        this.ws = null;             // Phase6: WebSocket instance
        this.useWebSocket = false;  // Phase6: true when WebSocket is active
        this.reconnectTimer = null;
    }

    connect() {
        // Phase6: try WebSocket first on each port, then fall back to HTTP polling
        this.tryConnectWebSocket();
    }

    // Phase6: attempt a WebSocket connection to ws://127.0.0.1:{port}/ws
    tryConnectWebSocket() {
        try {
            const port = APP_BASE_PORTS[this.portIndex];
            this.ws = new WebSocket("ws://127.0.0.1:" + port + "/ws");

            this.ws.onopen = () => {
                this.connected = true;
                this.useWebSocket = true;
                this.logger.log("WebSocket connected on port " + port);
                // Send initial sync
                this.ws.send(JSON.stringify({ path: "/sync", body: "" }));
                // Keep alive ping
                this.pingInterval = setInterval(() => {
                    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
                        this.ws.send(JSON.stringify({ path: "/ping", body: "" }));
                    }
                }, 10000);
            };

            this.ws.onmessage = (event) => {
                try {
                    const json = JSON.parse(event.data);
                    this.onMessage(json);
                } catch (e) {
                    this.logger.log("WebSocket message parse error: " + e);
                }
            };

            this.ws.onclose = () => {
                if (this.pingInterval) clearInterval(this.pingInterval);
                this.logger.log("WebSocket closed");
                this.useWebSocket = false;
                this.connected = false;
                this.ws = null;
                // Fall back to HTTP polling
                this.startHttpPolling();
            };

            this.ws.onerror = (err) => {
                this.logger.log("WebSocket error on port " + port);
                // Try next port, then fall back to HTTP
                this.portIndex = (this.portIndex + 1) % APP_BASE_PORTS.length;
                if (this.portIndex !== 0) {
                    this.tryConnectWebSocket();
                } else {
                    this.startHttpPolling();
                }
            };
        } catch (e) {
            this.logger.log("WebSocket connect failed: " + e);
            this.startHttpPolling();
        }
    }

    // Phase6: legacy HTTP polling fallback (used when WebSocket is unavailable)
    startHttpPolling() {
        this.useWebSocket = false;
        httpBaseUrl = "http://127.0.0.1:" + APP_BASE_PORTS[this.portIndex];
        setInterval(this.onTimer.bind(this), 5000);
    }

    onTimer() {
        fetch(httpBaseUrl + "/sync")
            .then(this.onResponse.bind(this))
            .catch(err => {
                if (!this.connected) {
                    this.portIndex = (this.portIndex + 1) % APP_BASE_PORTS.length;
                    httpBaseUrl = "http://127.0.0.1:" + APP_BASE_PORTS[this.portIndex];
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
        // Phase6: prefer WebSocket for sending (bidirectional, no HTTP overhead)
        if (this.useWebSocket && this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.ws.send(JSON.stringify({ path: url, body: JSON.stringify(data) }));
            return;
        }
        // HTTP fallback
        fetch(httpBaseUrl + url, { method: "POST", body: JSON.stringify(data) })
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
