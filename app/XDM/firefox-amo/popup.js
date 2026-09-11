// © Mayanktaker Computers & Web Development | https://mayanktaker.com
class VideoPopup {
    constructor() {
        this.rawList = [];
        this.displayedGroups = [];
        this.filterQuery = "";
        this.currentScope = "current"; // "current" | "all"
        this.activeTabId = null;
        this.activeTabTitle = "";
        this.activeTabUrl = "";
        this.preferredQuality = "";
        this.convertToMp3 = true;
        this.mp3Bitrate = "320k";
        this.soundEnabled = false;
        this.healthInterval = null;
    }

    run() {
        document.addEventListener('DOMContentLoaded', this.onLoad.bind(this), false);
    }

    queryActiveTab(callback) {
        const handleTabs = (tabs) => {
            if (tabs && tabs.length > 0 && tabs[0]) {
                this.activeTabId = tabs[0].id != null ? String(tabs[0].id) : null;
                this.activeTabTitle = tabs[0].title || "";
                this.activeTabUrl = tabs[0].url || "";
                if (this.rawList && this.rawList.length > 0) {
                    this.applyFilter();
                }
            }
            if (callback) callback();
        };

        // In Firefox MV3, lastFocusedWindow reliably targets the browser window containing the active webpage
        try {
            chrome.tabs.query({ active: true, lastFocusedWindow: true }, (tabs) => {
                if (tabs && tabs.length > 0) {
                    handleTabs(tabs);
                } else {
                    chrome.tabs.query({ active: true, currentWindow: true }, handleTabs);
                }
            });
        } catch (_) {
            try {
                chrome.tabs.query({ active: true, currentWindow: true }, handleTabs);
            } catch (_) {
                if (callback) callback();
            }
        }
    }

    onLoad() {
        this.queryActiveTab();

        // Load saved user preferences: sound chime, preferred resolution tier, MP3 conversion & bitrate
        chrome.storage.local.get(["fetchflowSoundEnabled", "fetchflowPreferredQuality", "fetchflowConvertToMp3", "fetchflowMp3Bitrate"], (res) => {
            if (res) {
                this.soundEnabled = !!res.fetchflowSoundEnabled;
                this.preferredQuality = res.fetchflowPreferredQuality || "";
                this.convertToMp3 = res.fetchflowConvertToMp3 !== undefined ? !!res.fetchflowConvertToMp3 : true;
                this.mp3Bitrate = res.fetchflowMp3Bitrate || "320k";
                const chkMp3 = document.getElementById("chkMp3");
                if (chkMp3) chkMp3.checked = this.convertToMp3;
                const mp3Row = document.getElementById("mp3BitrateRow");
                if (mp3Row) mp3Row.style.display = this.convertToMp3 ? "flex" : "none";
                const bitrateSelect = document.getElementById("mp3BitrateSelect");
                if (bitrateSelect) bitrateSelect.value = this.mp3Bitrate;
                this.updateSoundIcon();
                if (this.rawList && this.rawList.length > 0) {
                    this.applyFilter();
                }
            }
        });

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

        const chkMp3 = document.getElementById("chkMp3");
        if (chkMp3) {
            chkMp3.addEventListener('change', () => {
                this.convertToMp3 = chkMp3.checked;
                chrome.storage.local.set({ "fetchflowConvertToMp3": this.convertToMp3 });
                const mp3Row = document.getElementById("mp3BitrateRow");
                if (mp3Row) mp3Row.style.display = this.convertToMp3 ? "flex" : "none";
                this.showToast(this.convertToMp3 ? `Convert to MP3 (${this.mp3Bitrate})` : "Download original audio format");
                if (this.rawList && this.rawList.length > 0) {
                    this.applyFilter();
                }
            });
        }

        const bitrateSelect = document.getElementById("mp3BitrateSelect");
        if (bitrateSelect) {
            bitrateSelect.addEventListener('change', () => {
                this.mp3Bitrate = bitrateSelect.value || "320k";
                chrome.storage.local.set({ "fetchflowMp3Bitrate": this.mp3Bitrate });
                this.showToast(`MP3 bitrate set to ${this.mp3Bitrate}`);
                if (this.rawList && this.rawList.length > 0) {
                    this.applyFilter();
                }
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

        const refreshTabsBtn = document.getElementById("refreshTabsBtn");
        if (refreshTabsBtn) {
            refreshTabsBtn.addEventListener('click', () => {
                refreshTabsBtn.classList.add('spinning');
                this.queryActiveTab(() => {
                    chrome.runtime.sendMessage({ type: "stat" }, (res) => {
                        if (res) this.onMsg(res);
                        setTimeout(() => {
                            refreshTabsBtn.classList.remove('spinning');
                            this.showToast("Tab media refreshed");
                        }, 350);
                    });
                });
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
            const targetId = grp.selectedId || grp.items[0]?.id;
            const chosenItem = grp.items.find(it => String(it.id) === String(targetId)) || grp.items[0];
            const isAudio = chosenItem ? this.isAudioStream(chosenItem) : false;
            if (targetId) {
                setTimeout(() => {
                    chrome.runtime.sendMessage({
                        type: "vid",
                        itemId: targetId,
                        convertToMp3: isAudio ? this.convertToMp3 : false,
                        audioBitrate: isAudio ? this.mp3Bitrate : null
                    });
                }, idx * 120);
            }
        });
    }

    onMsg(response) {
        if (!response) return;

        if (response.health) {
            this.updateHealth(response.health);
        }

        // Only adopt background tab ID if we don't already have one from active browser query
        if (response.activeTabId != null && response.activeTabId !== "-1" && response.activeTabId !== -1) {
            if (!this.activeTabId) {
                this.activeTabId = String(response.activeTabId);
            }
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

    normalizeTitle(str) {
        if (!str) return "";
        return str
            .toLowerCase()
            .replace(/\.{2,}$/, "")
            .replace(/[-_]/g, " ")
            .replace(/\b(youtube|watch|official|video|audio|full|hd|mkv|mp4|webm|mhtml)\b/gi, "")
            .replace(/[^a-z0-9]/gi, "")
            .trim();
    }

    extractYoutubeId(url) {
        if (!url) return "";
        const m = url.match(/[?&]v=([a-zA-Z0-9_-]{11})/);
        return m ? m[1] : "";
    }

    matchesCurrentTab(group) {
        // 1. Direct Tab ID match: if any item in this group shares the active tab ID
        if (this.activeTabId && group.tabIds && group.tabIds.has(String(this.activeTabId))) {
            return true;
        }

        // 2. YouTube Video ID match: if active page URL shares YouTube video ID
        const currentYtId = this.extractYoutubeId(this.activeTabUrl);
        if (currentYtId && group.urls && group.urls.length > 0) {
            if (group.urls.some(u => u && u.includes(currentYtId))) {
                return true;
            }
        }

        // 3. Title match: active browser tab title matches the captured media title
        if (this.activeTabTitle && group.title) {
            const normTab = this.normalizeTitle(this.activeTabTitle);
            const normGrp = this.normalizeTitle(group.title);
            if (normTab && normGrp) {
                if (normTab.includes(normGrp) || normGrp.includes(normTab)) {
                    return true;
                }
                const sampleLength = Math.min(14, Math.min(normTab.length, normGrp.length));
                if (sampleLength >= 6 && normTab.slice(0, sampleLength) === normGrp.slice(0, sampleLength)) {
                    return true;
                }
            }
        }

        // 4. Fallback: if active tab could not be queried at all, show items
        if (!this.activeTabId && !this.activeTabTitle) {
            return true;
        }

        return false;
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
        if (this.isAudioStream({ text, info })) {
            const match = combined.match(/\b(\d{2,3})[Kk]\b/) || combined.match(/\b(\d{2,3})\s*KBPS\b/);
            return match ? parseInt(match[1], 10) : 1;
        }
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

        const bitrateMatch = combined.match(/\b(\d{2,3})[Kk]\b/) || combined.match(/\b(\d{2,3})\s*KBPS\b/);
        const bitrateSuffix = bitrateMatch ? ` · ${bitrateMatch[1]}k` : "";

        if (combined.includes("MP3")) return `MP3${bitrateSuffix}`;
        if (combined.includes("M4A")) return `M4A${bitrateSuffix}`;
        if (combined.includes("AAC")) return `AAC${bitrateSuffix}`;
        if (combined.includes("OPUS")) return `OPUS${bitrateSuffix}`;
        if (combined.includes("FLAC")) return `FLAC${bitrateSuffix}`;
        if (combined.includes("AUDIO ONLY") || combined.includes("AUDIO")) return `AUDIO${bitrateSuffix}`;
        if (combined.includes("MP4")) return "MP4";
        if (combined.includes("WEBM")) return combined.includes("AUDIO") ? `WEBM${bitrateSuffix}` : "WEBM";
        if (combined.includes("MKV")) return "MKV";
        return "VIDEO";
    }

    extractQualityKey(text, info) {
        const combined = ((text || "") + " " + (info || "")).toUpperCase();
        if (combined.includes("2160") || combined.includes("4K")) return "4K";
        if (combined.includes("1440") || combined.includes("2K")) return "2K";
        if (combined.includes("1080")) return "1080";
        if (combined.includes("720")) return "720";
        if (combined.includes("480")) return "480";
        if (combined.includes("360")) return "360";
        if (combined.includes("AUDIO")) return "AUDIO";
        return "";
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
            const rawInfo = (item.info || "").toUpperCase();

            // Drop any junk MHTML storyboard preview sheets
            if (rawInfo.includes("MHTML") || rawTitle.toLowerCase().endsWith(".mhtml") || rawInfo.includes("STORYBOARD")) {
                continue;
            }

            // Remove common container extensions (.mkv, .mp4, etc.) and trailing dots
            const cleanTitle = rawTitle
                .replace(/\.(mkv|mp4|webm|ts|m3u8|mpd|avi|flv|mov|m4a|mp3|aac|opus|flac|mhtml)$/i, "")
                .replace(/\.{2,}$/, "")
                .trim();
            // Group by normalized title — merges all resolutions of the same video into a single card
            const normTitle = this.normalizeTitle(cleanTitle) || cleanTitle.toLowerCase();
            const groupKey = normTitle;

            if (!groupsMap.has(groupKey)) {
                groupsMap.set(groupKey, {
                    title: cleanTitle || rawTitle,
                    rawTitle: rawTitle,
                    tabIds: new Set(),
                    urls: [],
                    items: []
                });
            }
            const grp = groupsMap.get(groupKey);
            if (item.tabId != null && item.tabId !== "" && item.tabId !== "-1" && item.tabId !== -1) {
                grp.tabIds.add(String(item.tabId));
            }
            if (item.url) grp.urls.push(item.url);
            if (item.tabUrl) grp.urls.push(item.tabUrl);
            grp.items.push(item);
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
        // Group all raw media items first into consolidated video entities
        const allGroups = this.groupMediaItems(this.rawList);

        // Filter groups for current tab using multi-signal matching
        const currentGroups = allGroups.filter(grp => this.matchesCurrentTab(grp));

        const scopeCurrentBadge = document.getElementById("scopeCurrentCount");
        const scopeAllBadge = document.getElementById("scopeAllCount");
        if (scopeCurrentBadge) scopeCurrentBadge.textContent = currentGroups.length + "";
        if (scopeAllBadge) scopeAllBadge.textContent = allGroups.length + "";

        // Handle empty current-tab prompt
        const emptyTabNotice = document.getElementById("emptyTabNotice");
        const emptyTabOtherCount = document.getElementById("emptyTabOtherCount");

        if (this.currentScope === 'current' && currentGroups.length === 0 && allGroups.length > 0) {
            if (emptyTabNotice) emptyTabNotice.style.display = 'flex';
            if (emptyTabOtherCount) emptyTabOtherCount.textContent = allGroups.length + "";
        } else {
            if (emptyTabNotice) emptyTabNotice.style.display = 'none';
        }

        // Apply scope selection
        let workingGroups = this.currentScope === 'current' ? currentGroups : allGroups;

        // Apply search query filter if user typed text
        if (this.filterQuery) {
            workingGroups = workingGroups.filter(grp => {
                const titleMatch = (grp.title || "").toLowerCase().includes(this.filterQuery);
                const formatMatch = grp.items.some(it => ((it.info || "") + " " + (it.text || "")).toLowerCase().includes(this.filterQuery));
                return titleMatch || formatMatch;
            });
        }

        this.displayedGroups = workingGroups;

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

    triggerDownloadItem(card, id, text, triggerBtn, isAudio = false) {
        if (card) {
            card.classList.add('media-card-downloading');
            setTimeout(() => card.classList.remove('media-card-downloading'), 600);
        }

        // Reassuring button confirmation state
        if (triggerBtn) {
            const originalHtml = triggerBtn.innerHTML;
            triggerBtn.classList.add('btn-queued');
            triggerBtn.innerHTML = `
                <svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2.5">
                    <polyline points="20 6 9 17 4 12"></polyline>
                </svg>
                <span>Queued</span>
            `;
            setTimeout(() => {
                triggerBtn.classList.remove('btn-queued');
                triggerBtn.innerHTML = originalHtml;
            }, 1400);
        }

        const shortName = text && text.length > 28 ? text.substring(0, 25) + '...' : (text || 'Media');
        const audioNote = isAudio && this.convertToMp3 ? ` (MP3 · ${this.mp3Bitrate})` : "";
        this.showToast(`Starting download: ${shortName}${audioNote}`);
        chrome.runtime.sendMessage({
            type: "vid",
            itemId: id,
            convertToMp3: isAudio ? this.convertToMp3 : false,
            audioBitrate: isAudio ? this.mp3Bitrate : null
        });
    }

    getFormatOptionLabel(item, isBest) {
        const info = (item.info || "").trim();
        const badge = this.getFormatBadge(item.text, item.info);
        let label = info ? `${info}` : badge;
        if (this.isAudioStream(item) && this.convertToMp3 && !label.toUpperCase().includes("MP3")) {
            label += ` (→ MP3 ${this.mp3Bitrate})`;
        }
        if (isBest) {
            label += " ★ Best Quality";
        }
        return label;
    }

    createGroupedCard(group) {
        const isAudioOnly = group.items.every(it => this.isAudioStream(it));

        // Find if user has a matching quality preference in this group
        let defaultIndex = 0;
        if (this.preferredQuality) {
            const preferredIdx = group.items.findIndex(it => {
                const key = this.extractQualityKey(it.text, it.info);
                return key && key.toUpperCase() === this.preferredQuality.toUpperCase();
            });
            if (preferredIdx !== -1) {
                defaultIndex = preferredIdx;
            }
        }

        const initialSelectedItem = group.items[defaultIndex];
        group.selectedId = initialSelectedItem.id;
        const initialBadge = this.getFormatBadge(initialSelectedItem.text, initialSelectedItem.info);

        const card = document.createElement('div');
        card.className = `media-card-grouped${isAudioOnly ? ' media-card-audio' : ''}`;
        card.setAttribute('title', group.title);

        // Header: Badge + Title + Copy Action
        const headerRow = document.createElement('div');
        headerRow.className = 'media-card-header';

        const badgeElem = document.createElement('div');
        const isInitialAudio = this.isAudioStream(initialSelectedItem);
        badgeElem.className = `media-card-badge${isInitialAudio ? ' media-card-badge-audio' : ''}`;
        badgeElem.textContent = initialBadge;

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
            if (idx === defaultIndex) {
                opt.selected = true;
            }
            select.appendChild(opt);
        });

        // Dynamic Badge update + Preference memory on dropdown change
        select.addEventListener('change', () => {
            const chosenId = select.value;
            group.selectedId = chosenId;
            const chosenItem = group.items.find(it => String(it.id) === String(chosenId));
            if (chosenItem) {
                const newBadge = this.getFormatBadge(chosenItem.text, chosenItem.info);
                const isChosenAudio = this.isAudioStream(chosenItem);
                badgeElem.textContent = newBadge;
                badgeElem.className = `media-card-badge${isChosenAudio ? ' media-card-badge-audio' : ''}`;

                // Persist preferred resolution tier across sessions
                const qualityKey = this.extractQualityKey(chosenItem.text, chosenItem.info);
                if (qualityKey) {
                    this.preferredQuality = qualityKey;
                    chrome.storage.local.set({ "fetchflowPreferredQuality": qualityKey });
                }
            }
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
            const chosenItem = group.items.find(it => String(it.id) === String(selectedId));
            const isAudio = chosenItem ? this.isAudioStream(chosenItem) : false;
            this.triggerDownloadItem(card, selectedId, group.title, dlBtn, isAudio);
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
                this.triggerDownloadItem(card, group.audioItem.id, `${group.title} (Audio)`, audioBtn, true);
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
