// © Mayanktaker Computers & Web Development | https://mayanktaker.com
class VideoPopup {
    constructor() {
        this.rawList = [];
        this.displayedGroups = [];
        this.filterQuery = "";
        this.currentScope = "current"; // "current" | "all"
        this.activeTabId = null;
        this.soundEnabled = false;
        this.healthInterval = null;
    }

    run() {
        document.addEventListener('DOMContentLoaded', this.onLoad.bind(this), false);
    }

    onLoad() {
        // Query active browser tab immediately for tab-scoping accuracy
        try {
            chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
                if (tabs && tabs.length > 0 && tabs[0].id != null) {
                    this.activeTabId = tabs[0].id + "";
                    if (this.rawList && this.rawList.length > 0) {
                        this.applyFilter();
                    }
                }
            });
        } catch (_) {}

        // Request initial status from background script
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

        // Load audio chime setting
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

        // Scope Switcher listeners
        const tabCurrentBtn = document.getElementById("scopeCurrentTab");
        const tabAllBtn = document.getElementById("scopeAllTabs");
        const switchToAllTabsBtn = document.getElementById("switchToAllTabsBtn");

        if (tabCurrentBtn) {
            tabCurrentBtn.addEventListener('click', () => this.setScope('current'));
        }
        if (tabAllBtn) {
            tabAllBtn.addEventListener('click', () => this.setScope('all'));
        }
        if (switchToAllTabsBtn) {
            switchToAllTabsBtn.addEventListener('click', () => this.setScope('all'));
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

        // End-to-end capture test
        const captureTestBtn = document.getElementById("captureTestBtn");
        if (captureTestBtn) {
            captureTestBtn.addEventListener('click', () => {
                chrome.runtime.sendMessage({ type: "capture-test" }, (res) => {
                    if (res && res.ok) {
                        this.showToast("Test download launched — if capture works, FetchFlow opens with it");
                    } else {
                        this.showToast((res && res.error) || "Capture test failed — monitoring disabled?");
                    }
                });
            });
        }
    }

    setScope(scope) {
        this.currentScope = scope;
        const tabCurrentBtn = document.getElementById("scopeCurrentTab");
        const tabAllBtn = document.getElementById("scopeAllTabs");

        if (tabCurrentBtn && tabAllBtn) {
            if (scope === 'current') {
                tabCurrentBtn.classList.add('active');
                tabCurrentBtn.setAttribute('aria-selected', 'true');
                tabAllBtn.classList.remove('active');
                tabAllBtn.setAttribute('aria-selected', 'false');
            } else {
                tabAllBtn.classList.add('active');
                tabAllBtn.setAttribute('aria-selected', 'true');
                tabCurrentBtn.classList.remove('active');
                tabCurrentBtn.setAttribute('aria-selected', 'false');
            }
        }
        this.applyFilter();
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
            osc.frequency.setValueAtTime(523.25, now);
            osc.frequency.setValueAtTime(659.25, now + 0.08);

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
        const groups = this.displayedGroups || [];
        if (groups.length === 0) {
            this.showToast("No media streams to download");
            return;
        }

        this.showToast(`Starting ${groups.length} video download${groups.length > 1 ? 's' : ''}...`);

        // Download the best/selected resolution for each distinct video group
        groups.forEach((grp, idx) => {
            const bestId = grp.items[0]?.id;
            if (bestId) {
                setTimeout(() => {
                    chrome.runtime.sendMessage({ type: "vid", itemId: bestId });
                }, idx * 120);
            }
        });
    }

    onMsg(response) {
        if (!response) return;

        if (response.health) {
            this.updateHealth(response.health);
        }

        if (response.activeTabId != null) {
            this.activeTabId = response.activeTabId + "";
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

    matchesCurrentTab(item) {
        if (!this.activeTabId) return true;
        if (item.tabId == null) return true;
        return String(item.tabId) === String(this.activeTabId);
    }

    getResolutionRank(text, info) {
        const combined = ((text || "") + " " + (info || "")).toUpperCase();
        if (combined.includes("2160") || combined.includes("4K")) return 2160;
        if (combined.includes("1440") || combined.includes("2K")) return 1440;
        if (combined.includes("1080")) return 1080;
        if (combined.includes("720")) return 720;
        if (combined.includes("480")) return 480;
        if (combined.includes("360")) return 360;
        if (combined.includes("240")) return 240;
        if (combined.includes("144")) return 144;
        if (this.isAudioStream({ text, info })) return 1;
        return 500;
    }

    getFormatBadge(text, info) {
        const combined = ((text || "") + " " + (info || "")).toUpperCase();
        if (combined.includes("4K") || combined.includes("2160")) return "4K";
        if (combined.includes("1440") || combined.includes("2K")) return "2K";
        if (combined.includes("1080P") || combined.includes("1080")) return "1080P";
        if (combined.includes("720P") || combined.includes("720")) return "720P";
        if (combined.includes("480P") || combined.includes("480")) return "480P";
        if (combined.includes("360P") || combined.includes("360")) return "360P";
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
        return "VIDEO";
    }

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

    groupMediaItems(items) {
        const groupsMap = new Map();

        for (const item of items) {
            const rawTitle = (item.text || "Untitled Media").trim();
            // Remove common container extensions from base grouping title
            const cleanTitle = rawTitle.replace(/\.(mkv|mp4|webm|ts|m3u8|mpd|avi|flv|mov|m4a|mp3|aac|opus|flac)$/i, "").trim();
            const tabKey = item.tabId != null ? String(item.tabId) : "unknown";
            const groupKey = `${tabKey}:::${cleanTitle.toLowerCase()}`;

            if (!groupsMap.has(groupKey)) {
                groupsMap.set(groupKey, {
                    title: cleanTitle || rawTitle,
                    rawTitle: rawTitle,
                    tabId: item.tabId,
                    items: []
                });
            }
            groupsMap.get(groupKey).items.push(item);
        }

        const groups = [];
        for (const group of groupsMap.values()) {
            // Sort items in this video group descending by quality/resolution
            group.items.sort((a, b) => {
                const rankA = this.getResolutionRank(a.text, a.info);
                const rankB = this.getResolutionRank(b.text, b.info);
                return rankB - rankA;
            });

            const topItem = group.items[0];
            group.topBadge = this.getFormatBadge(topItem.text, topItem.info);
            group.audioItem = group.items.find(it => this.isAudioStream(it));
            groups.push(group);
        }

        return groups;
    }

    applyFilter() {
        // Calculate counts for the Scope switcher
        const currentItems = this.rawList.filter(item => this.matchesCurrentTab(item));
        const allItems = this.rawList;

        const currentGroups = this.groupMediaItems(currentItems);
        const allGroups = this.groupMediaItems(allItems);

        const scopeCurrentBadge = document.getElementById("scopeCurrentCount");
        const scopeAllBadge = document.getElementById("scopeAllCount");
        if (scopeCurrentBadge) scopeCurrentBadge.textContent = currentGroups.length + "";
        if (scopeAllBadge) scopeAllBadge.textContent = allGroups.length + "";

        // Handle empty current-tab prompt
        const emptyTabNotice = document.getElementById("emptyTabNotice");
        const emptyTabOtherCount = document.getElementById("emptyTabOtherCount");

        if (this.currentScope === 'current' && currentItems.length === 0 && allItems.length > 0) {
            if (emptyTabNotice) emptyTabNotice.style.display = 'flex';
            if (emptyTabOtherCount) emptyTabOtherCount.textContent = allGroups.length + "";
        } else {
            if (emptyTabNotice) emptyTabNotice.style.display = 'none';
        }

        // Apply scope selection
        let workingList = this.currentScope === 'current' ? currentItems : allItems;

        // Apply search query filter if user typed text
        if (this.filterQuery) {
            workingList = workingList.filter(item => {
                const text = (item.text || "").toLowerCase();
                const info = (item.info || "").toLowerCase();
                return text.includes(this.filterQuery) || info.includes(this.filterQuery);
            });
        }

        // Group the filtered items into consolidated video entities
        this.displayedGroups = this.groupMediaItems(workingList);

        const downloadAllBtn = document.getElementById("downloadAll");
        if (downloadAllBtn) {
            downloadAllBtn.disabled = this.displayedGroups.length === 0;
            downloadAllBtn.innerHTML = `
                <svg class="btn-icon" viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2.2">
                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>
                </svg>
                ${this.displayedGroups.length > 0 ? `Download All (${this.displayedGroups.length})` : 'Download All'}
            `;
        }

        this.renderGroups(this.displayedGroups);
    }

    updateHealth(health) {
        const pill = document.getElementById("healthPill");
        const dot = document.getElementById("healthDot");
        const text = document.getElementById("healthText");
        if (!pill || !dot || !text) return;

        if (!pill._hasReconnectHandler) {
            pill._hasReconnectHandler = true;
            pill.addEventListener('click', () => {
                if (pill.classList.contains('health-offline')) {
                    this.showToast("Retrying connection to FetchFlow...");
                    chrome.runtime.sendMessage({ type: "reconnect" }, (resp) => {
                        if (resp && resp.health) this.updateHealth(resp.health);
                    });
                }
            });
        }

        if (!health || !health.connected) {
            pill.className = "health-pill health-offline";
            dot.className = "health-dot health-dot-offline";
            const retryIn = health && health.retryIn != null ? health.retryIn : null;
            if (retryIn != null && retryIn > 0) {
                text.textContent = `Offline · Retry ${retryIn}s`;
            } else {
                text.textContent = "Offline · Retry";
            }
            pill.title = "Disconnected from FetchFlow Core (Click to reconnect immediately)";
            return;
        }

        if (health.useWebSocket) {
            const isLag = health.latency != null && health.latency > 100;
            pill.className = `health-pill health-ws${isLag ? ' health-lag' : ''}`;
            dot.className = `health-dot ${isLag ? 'health-dot-lag' : 'health-dot-ws'}`;
            const latencyStr = health.latency != null ? `${health.latency}ms` : "Active";
            text.textContent = isLag ? `WS · ${latencyStr} (lag)` : `WS · ${latencyStr}`;
            pill.title = `WebSocket Port ${health.port || 8597} | Latency: ${latencyStr}${isLag ? ' (high latency detected)' : ''}`;
        } else {
            pill.className = "health-pill health-http";
            dot.className = "health-dot health-dot-http";
            text.textContent = "HTTP · Polling";
            pill.title = `Connected via HTTP fallback on Port ${health.port || 8597}`;
        }
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

    triggerDownloadItem(card, id, text) {
        if (card) {
            card.classList.add('media-card-downloading');
            setTimeout(() => card.classList.remove('media-card-downloading'), 600);
        }
        const shortName = text && text.length > 28 ? text.substring(0, 25) + '...' : (text || 'Media');
        this.showToast(`Starting download: ${shortName}`);
        chrome.runtime.sendMessage({ type: "vid", itemId: id });
    }

    getFormatOptionLabel(item, isBest) {
        const info = (item.info || "").trim();
        const badge = this.getFormatBadge(item.text, item.info);
        let label = info ? `${info}` : badge;
        if (isBest) {
            label += " ★ Best Quality";
        }
        return label;
    }

    createGroupedCard(group) {
        const isAudioOnly = group.items.every(it => this.isAudioStream(it));
        const badge = group.topBadge || (isAudioOnly ? "AUDIO" : "VIDEO");

        const card = document.createElement('div');
        card.className = `media-card-grouped${isAudioOnly ? ' media-card-audio' : ''}`;
        card.setAttribute('title', group.title);

        // Header: Badge + Title + Copy Action
        const headerRow = document.createElement('div');
        headerRow.className = 'media-card-header';

        const badgeElem = document.createElement('div');
        badgeElem.className = `media-card-badge${isAudioOnly ? ' media-card-badge-audio' : ''}`;
        badgeElem.textContent = badge;

        const titleElem = document.createElement('div');
        titleElem.className = 'media-card-header-title';
        titleElem.textContent = group.title;

        const actionsWrap = document.createElement('div');
        actionsWrap.className = 'media-card-header-actions';

        const copyBtn = document.createElement('button');
        copyBtn.className = 'media-card-btn-copy';
        copyBtn.setAttribute('title', 'Copy video title to clipboard');
        copyBtn.setAttribute('aria-label', `Copy title for ${group.title}`);
        copyBtn.innerHTML = `
            <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2">
                <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
            </svg>
        `;
        copyBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(group.title);
                this.showToast("Title copied to clipboard!");
            }
        });
        actionsWrap.appendChild(copyBtn);

        headerRow.appendChild(badgeElem);
        headerRow.appendChild(titleElem);
        headerRow.appendChild(actionsWrap);

        // Selector Row: Quality Dropdown + Primary Download Button + Quick Audio Chip
        const selectorRow = document.createElement('div');
        selectorRow.className = 'media-card-selector-row';

        const select = document.createElement('select');
        select.className = 'quality-select';
        select.setAttribute('aria-label', `Select quality for ${group.title}`);

        group.items.forEach((it, idx) => {
            const opt = document.createElement('option');
            opt.value = it.id;
            opt.textContent = this.getFormatOptionLabel(it, idx === 0);
            select.appendChild(opt);
        });

        const dlBtn = document.createElement('button');
        dlBtn.className = 'btn btn-primary btn-download-group';
        dlBtn.setAttribute('title', `Download selected quality`);
        dlBtn.setAttribute('aria-label', `Download ${group.title}`);
        dlBtn.innerHTML = `
            <svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2.2">
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>
            </svg>
            <span>Download</span>
        `;
        dlBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            const selectedId = select.value;
            this.triggerDownloadItem(card, selectedId, group.title);
        });

        selectorRow.appendChild(select);
        selectorRow.appendChild(dlBtn);

        // Optional Quick Audio button when an audio track exists in a video group
        if (group.audioItem && !isAudioOnly) {
            const audioBtn = document.createElement('button');
            audioBtn.className = 'btn-quick-audio';
            audioBtn.setAttribute('title', 'Download audio track directly (MP3/M4A)');
            audioBtn.setAttribute('aria-label', `Download audio for ${group.title}`);
            audioBtn.innerHTML = `
                <svg viewBox="0 0 24 24" width="11" height="11" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M9 18V5l12-2v13"></path><circle cx="6" cy="18" r="3"></circle><circle cx="18" cy="16" r="3"></circle>
                </svg>
                <span>Audio</span>
            `;
            audioBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                this.triggerDownloadItem(card, group.audioItem.id, `${group.title} (Audio)`);
            });
            selectorRow.appendChild(audioBtn);
        }

        card.appendChild(headerRow);
        card.appendChild(selectorRow);

        return card;
    }

    renderGroups(groups) {
        const listContainer = document.getElementById("list");
        if (!listContainer) return;
        listContainer.innerHTML = '';

        if (groups.length === 0) {
            const noMatch = document.createElement('div');
            noMatch.className = 'no-match-message';
            noMatch.textContent = this.filterQuery
                ? "No media matching search filter."
                : (this.currentScope === 'current' ? "No media captured on this tab yet." : "No media detected.");
            listContainer.appendChild(noMatch);
            return;
        }

        groups.forEach(group => {
            listContainer.appendChild(this.createGroupedCard(group));
        });
    }
}

const popup = new VideoPopup();
popup.run();
