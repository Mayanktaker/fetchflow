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
        this.lastGoodPortIndex = null;   // prefer the port that last answered
        this.ws = null;                  // Phase6: WebSocket instance
        this.useWebSocket = false;       // Phase6: true when WebSocket is active
        this.reconnectTimer = null;
        this.nextRetryTime = null;
        this.reconnectInterval = 3000;   // 3 seconds retry interval
        this.pollingStarted = false;
        this.pollingTimer = null;
        this.lastPingSentTime = null;
        this.latency = null;
        this.attemptId = 0;              // single-flight guard: only the newest socket's callbacks act
    }

    connect() {
        // Phase6: try WebSocket first on each port, then fall back to HTTP polling
        this.tryConnectWebSocket();
    }

    // Schedule next reconnect attempt with countdown tracking
    scheduleReconnect() {
        if (this.reconnectTimer) clearTimeout(this.reconnectTimer);
        this.nextRetryTime = Date.now() + this.reconnectInterval;
        this.reconnectTimer = setTimeout(() => {
            this.reconnectTimer = null;
            this.nextRetryTime = null;
            this.tryConnectWebSocket();
        }, this.reconnectInterval);
    }

    // Force an immediate reconnect attempt (any live socket is closed safely
    // inside tryConnectWebSocket AFTER the attempt id is bumped, so its onclose
    // cannot fire the reconnect dance a second time)
    reconnectNow() {
        if (this.reconnectTimer) clearTimeout(this.reconnectTimer);
        this.reconnectTimer = null;
        this.nextRetryTime = null;
        this.tryConnectWebSocket();
    }

    // Close the active WebSocket (if any) so a superseded socket cannot fire stale callbacks
    closeCurrentSocket() {
        if (this.ws) {
            const stale = this.ws;
            this.ws = null;
            try { stale.close(); } catch (e) { }
        }
    }

    // Measure live WebSocket latency on demand
    pingNow() {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.lastPingSentTime = Date.now();
            this.ws.send(JSON.stringify({ path: "/ping", body: "" }));
        }
    }

    // Returns current connection health metrics for popup consumption
    getHealthInfo() {
        const retryIn = this.nextRetryTime ? Math.max(0, Math.ceil((this.nextRetryTime - Date.now()) / 1000)) : null;
        return {
            connected: !!this.connected,
            useWebSocket: !!this.useWebSocket,
            latency: this.latency,
            port: APP_BASE_PORTS[this.portIndex],
            retryIn: retryIn
        };
    }

    // Phase6: attempt a WebSocket connection to ws://127.0.0.1:{port}/ws
    tryConnectWebSocket() {
        const attempt = ++this.attemptId;
        // Single live socket: close any prior attempt; its callbacks are now stale
        this.closeCurrentSocket();
        try {
            const port = APP_BASE_PORTS[this.portIndex];
            const ws = new WebSocket("ws://127.0.0.1:" + port + "/ws");
            this.ws = ws;
            this.logger.log("WebSocket attempt #" + attempt + " on port " + port);

            ws.onopen = () => {
                if (attempt !== this.attemptId) return; // superseded socket
                if (this.reconnectTimer) clearTimeout(this.reconnectTimer);
                this.reconnectTimer = null;
                this.nextRetryTime = null;
                this.connected = true;
                this.useWebSocket = true;
                this.lastGoodPortIndex = this.portIndex;
                // CRITICAL: update httpBaseUrl so postBlobChunk uses the correct port
                httpBaseUrl = "http://127.0.0.1:" + port;
                this.logger.log("WebSocket connected on port " + port + " | httpBaseUrl: " + httpBaseUrl);
                // WS is authoritative now — stop the HTTP polling bridge entirely
                this.stopHttpPolling();
                // Send initial sync and measure round-trip latency
                this.lastPingSentTime = Date.now();
                ws.send(JSON.stringify({ path: "/sync", body: "" }));
                // Keep alive ping every 5 seconds for fresh latency readings
                this.pingInterval = setInterval(() => {
                    if (this.ws === ws && ws.readyState === WebSocket.OPEN) {
                        this.lastPingSentTime = Date.now();
                        ws.send(JSON.stringify({ path: "/ping", body: "" }));
                    }
                }, 5000);
            };

            ws.onmessage = (event) => {
                if (attempt !== this.attemptId) return;
                if (this.lastPingSentTime) {
                    this.latency = Math.max(1, Date.now() - this.lastPingSentTime);
                    this.lastPingSentTime = null;
                }
                try {
                    const json = JSON.parse(event.data);
                    this.onMessage(json);
                } catch (e) {
                    this.logger.log("WebSocket message parse error: " + e);
                }
            };

            ws.onclose = () => {
                if (attempt !== this.attemptId) return; // stale socket closed — ignore
                if (this.pingInterval) clearInterval(this.pingInterval);
                this.pingInterval = null;
                this.logger.log("WebSocket closed on port " + port);
                this.useWebSocket = false;
                this.connected = false;
                if (this.ws === ws) this.ws = null;
                this.latency = null;
                // Walk the range round-robin, preferring the last port that answered
                this.portIndex = this.lastGoodPortIndex !== null ? this.lastGoodPortIndex : this.portIndex;
                this.portIndex = (this.portIndex + 1) % APP_BASE_PORTS.length;
                httpBaseUrl = "http://127.0.0.1:" + APP_BASE_PORTS[this.portIndex];
                this.scheduleReconnect();
                // Bridge with HTTP polling until the WebSocket comes back
                this.startHttpPolling();
            };

            ws.onerror = () => {
                // Handled by onclose (always follows onerror) — single-flight: no recursion here
                this.logger.log("WebSocket error on port " + port);
                if (attempt === this.attemptId) this.latency = null;
            };
        } catch (e) {
            this.logger.log("WebSocket connect failed: " + e);
            this.latency = null;
            this.startHttpPolling();
        }
    }

    // Phase6: legacy HTTP polling fallback (used when WebSocket is unavailable)
    startHttpPolling() {
        this.useWebSocket = false;
        httpBaseUrl = "http://127.0.0.1:" + APP_BASE_PORTS[this.lastGoodPortIndex !== null ? this.lastGoodPortIndex : this.portIndex];
        if (this.pollingStarted) {
            return;
        }
        this.pollingStarted = true;
        this.pollingTimer = setInterval(this.onTimer.bind(this), 5000);
        // Poll immediately — a cold-woken background must not wait out the first
        // interval before it can deliver queued downloads.
        this.onTimer();
    }

    // Stop the HTTP polling bridge once the WebSocket is authoritative again
    stopHttpPolling() {
        if (this.pollingTimer) clearInterval(this.pollingTimer);
        this.pollingTimer = null;
        this.pollingStarted = false;
    }

    onTimer() {
        fetch(httpBaseUrl + "/sync")
            .then(this.onResponse.bind(this))
            .catch(err => {
                // Never flip a healthy WebSocket connection offline because one
                // poll failed — polling is only a bridge while WS is down.
                if (this.useWebSocket) {
                    return;
                }
                this.portIndex = (this.portIndex + 1) % APP_BASE_PORTS.length;
                httpBaseUrl = "http://127.0.0.1:" + APP_BASE_PORTS[this.portIndex];
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
            .catch(err => {
                if (!this.useWebSocket) this.disconnect();
            });
    }

    // Blob chunk upload: raw binary POST to /blob-upload (not JSON)
    async postBlobChunk(headers, chunkBytes) {
        const url = httpBaseUrl + "/blob-upload";
        this.logger.log("[connector] POST " + url + " | chunk bytes: " + chunkBytes.length + " | X-Blob-Transfer-Id: " + headers["X-Blob-Transfer-Id"]);
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/octet-stream",
                ...headers
            },
            body: chunkBytes
        });
        this.logger.log("[connector] Response status: " + response.status + " " + response.statusText);
        if (!response.ok) throw new Error("blob-upload HTTP " + response.status);
        return response.json();
    }

    // Launches FetchFlow via OS-registered fetchflow:// URL scheme.
    // Cooldown: each offline download used to spawn another app process (log showed
    // instance storms every few minutes) — one launch per minute is plenty.
    launchApp() {
        const now = Date.now();
        if (this.lastLaunchTime && now - this.lastLaunchTime < 60000) {
            this.logger.log("launchApp suppressed (cooldown)");
            return;
        }
        this.lastLaunchTime = now;
        try {
            chrome.tabs.create({ url: "fetchflow://launch" });
        } catch (e) {
            this.logger.log("launchApp failed: " + e);
        }
    }
}
