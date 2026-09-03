// © Mayanktaker Computers & Web Development | https://mayanktaker.com
class VideoPopup {
    constructor() {
        this.rawList = [];
        this.filteredList = [];
        this.filterQuery = "";
        this.soundEnabled = false;
        this.healthInterval = null;
    }

    run() {
        document.addEventListener('DOMContentLoaded', this.onLoad.bind(this), false);
    }

    onLoad() {
        chrome.runtime.sendMessage({ type: "stat" }, this.onMsg.bind(this));

        // Periodic ping to keep WebSocket health latency fresh while popup is open
        this.healthInterval = setInterval(() => {
            chrome.runtime.sendMessage({ type: "ping" }, (res) => {
                if (res && res.health) {
                    this.updateHealth(res.health);
                }
            });
        }, 2500);

        window.addEventListener('unload', () => {
            if (this.healthInterval) clearInterval(this.healthInterval);
        });

        // Load sound setting
        chrome.storage.local.get(["fetchflowSoundEnabled"], (res) => {
            this.soundEnabled = !!res.fetchflowSoundEnabled;
            this.updateSoundIcon();
        });

        const soundToggle = document.getElementById("soundToggle");
        if (soundToggle) {
            soundToggle.addEventListener('click', () => {
                this.soundEnabled = !this.soundEnabled;
                chrome.storage.local.set({ "fetchflowSoundEnabled": this.soundEnabled });
                this.updateSoundIcon();
                if (this.soundEnabled) {
                    this.playAudioChime();
                    this.showToast("Sound chime enabled");
                } else {
                    this.showToast("Sound chime muted");
                }
            });
        }

        const chk = document.getElementById("chk");
        if (chk) {
            chk.addEventListener('change', () => {
                chrome.runtime.sendMessage({ type: "cmd", enabled: chk.checked });
            });
        }

        const searchInput = document.getElementById("searchInput");
        const clearSearchBtn = document.getElementById("clearSearch");

        if (searchInput) {
            searchInput.addEventListener('input', (e) => {
                this.filterQuery = (e.target.value || "").trim().toLowerCase();
                if (clearSearchBtn) {
                    clearSearchBtn.style.display = this.filterQuery ? "inline-flex" : "none";
                }
                this.applyFilter();
            });
        }

        if (clearSearchBtn) {
            clearSearchBtn.addEventListener('click', () => {
                if (searchInput) {
                    searchInput.value = "";
                    this.filterQuery = "";
                    clearSearchBtn.style.display = "none";
                    this.applyFilter();
                    searchInput.focus();
                }
            });
        }

        const downloadAllBtn = document.getElementById("downloadAll");
        if (downloadAllBtn) {
            downloadAllBtn.addEventListener('click', () => {
                this.downloadAllFiltered();
            });
        }
    }

    updateSoundIcon() {
        const onIcon = document.getElementById("soundIconOn");
        const offIcon = document.getElementById("soundIconOff");
        if (onIcon && offIcon) {
            onIcon.style.display = this.soundEnabled ? "block" : "none";
            offIcon.style.display = this.soundEnabled ? "none" : "block";
        }
    }

    playAudioChime() {
        try {
            const AudioCtx = window.AudioContext || window.webkitAudioContext;
            if (!AudioCtx) return;
            const ctx = new AudioCtx();
            const now = ctx.currentTime;
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();

            osc.type = "sine";
            osc.frequency.setValueAtTime(523.25, now); // C5
            osc.frequency.setValueAtTime(659.25, now + 0.08); // E5

            gain.gain.setValueAtTime(0.001, now);
            gain.gain.exponentialRampToValueAtTime(0.12, now + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.001, now + 0.22);

            osc.connect(gain);
            gain.connect(ctx.destination);

            osc.start(now);
            osc.stop(now + 0.23);
            osc.onended = () => ctx.close();
        } catch (_) {}
    }

    downloadAllFiltered() {
        const items = this.filteredList;
        if (!items || items.length === 0) {
            this.showToast("No media streams to download");
            return;
        }

        this.showToast(`Starting ${items.length} download${items.length > 1 ? 's' : ''}...`);

        // Trigger downloads with a staggered 120ms interval to ensure smooth IPC transmission
        items.forEach((item, idx) => {
            setTimeout(() => {
                chrome.runtime.sendMessage({ type: "vid", itemId: item.id });
            }, idx * 120);
        });
    }

    onMsg(response) {
        if (!response) return;

        if (response.health) {
            this.updateHealth(response.health);
        }

        const chk = document.getElementById("chk");
        if (chk) {
            chk.checked = !!response.enabled;
        }

        const clearBtn = document.getElementById('clear');
        if (clearBtn) {
            clearBtn.addEventListener('click', () => {
                chrome.runtime.sendMessage({ type: "clear" });
                window.close();
            });
        }

        const formatBtn = document.getElementById('format');
        if (formatBtn) {
            formatBtn.addEventListener('click', () => {
                alert("Please select and play the video in your desired quality in the web player to capture it.");
            });
        }

        this.rawList = response.list || [];
        const mediaContainer = document.getElementById('mediaContainer');
        const emptyState = document.getElementById('emptyState');

        if (this.rawList.length > 0) {
            if (mediaContainer) mediaContainer.style.display = 'block';
            if (emptyState) emptyState.style.display = 'none';
            this.applyFilter();
        } else {
            if (mediaContainer) mediaContainer.style.display = 'none';
            if (emptyState) emptyState.style.display = 'flex';
        }
    }

    applyFilter() {
        this.filteredList = this.filterQuery
            ? this.rawList.filter(item => {
                const text = (item.text || "").toLowerCase();
                const info = (item.info || "").toLowerCase();
                return text.includes(this.filterQuery) || info.includes(this.filterQuery);
            })
            : this.rawList;

        const downloadAllBtn = document.getElementById("downloadAll");
        if (downloadAllBtn) {
            downloadAllBtn.disabled = this.filteredList.length === 0;
            downloadAllBtn.innerHTML = `
                <svg class="btn-icon" viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2.2">
                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>
                </svg>
                ${this.filterQuery ? `Download (${this.filteredList.length})` : 'Download All'}
            `;
        }

        this.renderList(this.filteredList);
    }

    // Render connection health indicator pill with live WebSocket latency
    updateHealth(health) {
        const pill = document.getElementById("healthPill");
        const dot = document.getElementById("healthDot");
        const text = document.getElementById("healthText");
        if (!pill || !dot || !text) return;

        if (!health || !health.connected) {
            pill.className = "health-pill health-offline";
            dot.className = "health-dot health-dot-offline";
            text.textContent = "Offline";
            pill.title = "Disconnected from FetchFlow Core";
            return;
        }

        if (health.useWebSocket) {
            pill.className = "health-pill health-ws";
            dot.className = "health-dot health-dot-ws";
            const latencyStr = health.latency != null ? `${health.latency}ms` : "Active";
            text.textContent = `WS · ${latencyStr}`;
            pill.title = `WebSocket connected on port ${health.port || 8597} | Latency: ${latencyStr}`;
        } else {
            pill.className = "health-pill health-http";
            dot.className = "health-dot health-dot-http";
            text.textContent = "HTTP · Polling";
            pill.title = `Connected via HTTP fallback on port ${health.port || 8597}`;
        }
    }

    // Detect audio-only media streams based on extension, MIME, or format metadata
    isAudioStream(item) {
        const text = (item.text || "").toUpperCase();
        const info = (item.info || "").toUpperCase();
        const combined = text + " " + info;
        if (combined.includes("AUDIO ONLY") || combined.includes("AUDIO/")) return true;
        if (combined.includes(".MP3") || combined.includes(".M4A") || combined.includes(".AAC") ||
            combined.includes(".OPUS") || combined.includes(".OGG") || combined.includes(".FLAC") ||
            combined.includes(".WAV") || combined.includes(".WMA")) {
            return true;
        }
        if (info.includes("AUDIO") && !info.includes("VIDEO") && !info.includes("1080") &&
            !info.includes("720") && !info.includes("480") && !info.includes("360") &&
            !info.includes("2160") && !info.includes("4K")) {
            return true;
        }
        return false;
    }

    getFormatBadge(text, info) {
        const combined = (text + " " + info).toUpperCase();
        if (combined.includes("M3U8") || combined.includes("HLS")) return "HLS";
        if (combined.includes("MP3")) return "MP3";
        if (combined.includes("M4A")) return "M4A";
        if (combined.includes("AAC")) return "AAC";
        if (combined.includes("OPUS")) return "OPUS";
        if (combined.includes("FLAC")) return "FLAC";
        if (combined.includes("AUDIO ONLY") || combined.includes("AUDIO")) return "AUDIO";
        if (combined.includes("MP4")) return "MP4";
        if (combined.includes("WEBM")) return "WEBM";
        if (combined.includes("MKV")) return "MKV";
        if (combined.includes("1080P") || combined.includes("1080")) return "1080P";
        if (combined.includes("720P") || combined.includes("720")) return "720P";
        if (combined.includes("4K") || combined.includes("2160")) return "4K";
        return "VIDEO";
    }

    showToast(msg) {
        const toast = document.getElementById("copyToast");
        if (!toast) return;
        toast.textContent = msg || "Done";
        toast.style.display = "block";
        toast.style.opacity = "1";
        setTimeout(() => {
            toast.style.opacity = "0";
            setTimeout(() => { toast.style.display = "none"; }, 200);
        }, 1800);
    }

    createSectionHeader(title, iconSvg, count) {
        const header = document.createElement('div');
        header.className = 'media-section-header';

        const titleWrap = document.createElement('div');
        titleWrap.className = 'media-section-title-wrap';

        const iconSpan = document.createElement('span');
        iconSpan.className = 'media-section-icon';
        iconSpan.innerHTML = iconSvg;

        const titleSpan = document.createElement('span');
        titleSpan.className = 'media-section-title';
        titleSpan.textContent = title;

        titleWrap.appendChild(iconSpan);
        titleWrap.appendChild(titleSpan);

        const badgeSpan = document.createElement('span');
        badgeSpan.className = 'media-section-badge';
        badgeSpan.textContent = count + '';

        header.appendChild(titleWrap);
        header.appendChild(badgeSpan);
        return header;
    }

    createCard(listItem, isAudio) {
        const text = listItem.text || "Untitled Media";
        const info = listItem.info || "";
        const id = listItem.id;
        const badge = this.getFormatBadge(text, info);

        const card = document.createElement('div');
        card.className = `media-card${isAudio ? ' media-card-audio' : ''}`;
        card.setAttribute('role', 'button');
        card.setAttribute('tabindex', '0');
        card.setAttribute('title', text);

        const badgeElem = document.createElement('div');
        badgeElem.className = `media-card-badge${isAudio ? ' media-card-badge-audio' : ''}`;
        badgeElem.textContent = badge;

        const detailsElem = document.createElement('div');
        detailsElem.className = 'media-card-details';

        const titleElem = document.createElement('div');
        titleElem.className = 'media-card-title';
        titleElem.textContent = text;
        detailsElem.appendChild(titleElem);

        if (info) {
            const infoElem = document.createElement('div');
            infoElem.className = 'media-card-info';
            infoElem.textContent = info;
            detailsElem.appendChild(infoElem);
        }

        const actionsWrap = document.createElement('div');
        actionsWrap.className = 'media-card-actions';

        // Quick Copy Link button
        const copyBtn = document.createElement('button');
        copyBtn.className = 'media-card-btn-copy';
        copyBtn.setAttribute('title', 'Copy link to clipboard');
        copyBtn.setAttribute('aria-label', `Copy link for ${text}`);
        copyBtn.innerHTML = `
            <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2">
                <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
            </svg>
        `;
        copyBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(text);
                this.showToast("Link copied to clipboard!");
            }
        });

        // Download Trigger button
        const downloadBtn = document.createElement('button');
        downloadBtn.className = 'media-card-action';
        downloadBtn.setAttribute('title', 'Download with FetchFlow');
        downloadBtn.setAttribute('aria-label', `Download ${text}`);
        downloadBtn.innerHTML = `
            <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.2">
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>
            </svg>
        `;

        actionsWrap.appendChild(copyBtn);
        actionsWrap.appendChild(downloadBtn);

        card.appendChild(badgeElem);
        card.appendChild(detailsElem);
        card.appendChild(actionsWrap);

        card.addEventListener('click', () => {
            chrome.runtime.sendMessage({ type: "vid", itemId: id });
        });

        return card;
    }

    renderList(arr) {
        const listContainer = document.getElementById("list");
        if (!listContainer) return;
        listContainer.innerHTML = '';

        if (arr.length === 0) {
            const noMatch = document.createElement('div');
            noMatch.className = 'no-match-message';
            noMatch.textContent = "No media matching search filter.";
            listContainer.appendChild(noMatch);
            return;
        }

        const videoItems = [];
        const audioItems = [];

        // Distribute items into video and audio groupings (newest on top)
        for (let i = arr.length - 1; i >= 0; i--) {
            const item = arr[i];
            if (this.isAudioStream(item)) {
                audioItems.push(item);
            } else {
                videoItems.push(item);
            }
        }

        const videoIconSvg = `<svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2"><polygon points="23 7 16 12 23 17 23 7"></polygon><rect x="1" y="5" width="15" height="14" rx="2" ry="2"></rect></svg>`;
        const audioIconSvg = `<svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 18V5l12-2v13"></path><circle cx="6" cy="18" r="3"></circle><circle cx="18" cy="16" r="3"></circle></svg>`;

        // If both video and audio streams exist, display distinct group headers
        if (videoItems.length > 0 && audioItems.length > 0) {
            // Group 1: Video Streams
            const videoSection = document.createElement('div');
            videoSection.className = 'media-section';
            videoSection.appendChild(this.createSectionHeader("Video Streams", videoIconSvg, videoItems.length));
            videoItems.forEach(item => videoSection.appendChild(this.createCard(item, false)));
            listContainer.appendChild(videoSection);

            // Group 2: Audio Streams
            const audioSection = document.createElement('div');
            audioSection.className = 'media-section';
            audioSection.appendChild(this.createSectionHeader("Audio Streams", audioIconSvg, audioItems.length));
            audioItems.forEach(item => audioSection.appendChild(this.createCard(item, true)));
            listContainer.appendChild(audioSection);
        } else if (audioItems.length > 0) {
            // Only audio streams exist
            const audioSection = document.createElement('div');
            audioSection.className = 'media-section';
            audioSection.appendChild(this.createSectionHeader("Audio Streams", audioIconSvg, audioItems.length));
            audioItems.forEach(item => audioSection.appendChild(this.createCard(item, true)));
            listContainer.appendChild(audioSection);
        } else {
            // Only video streams exist
            videoItems.forEach(item => listContainer.appendChild(this.createCard(item, false)));
        }
    }
}

const popup = new VideoPopup();
popup.run();
