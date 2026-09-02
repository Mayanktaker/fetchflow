// © Mayanktaker Computers & Web Development | https://mayanktaker.com
class VideoPopup {
    run() {
        document.addEventListener('DOMContentLoaded', this.onLoad.bind(this), false);
    }

    onLoad() {
        chrome.runtime.sendMessage({ type: "stat" }, this.onMsg.bind(this));

        const chk = document.getElementById("chk");
        if (chk) {
            chk.addEventListener('change', () => {
                chrome.runtime.sendMessage({ type: "cmd", enabled: chk.checked });
            });
        }
    }

    onMsg(response) {
        if (!response) return;

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

        const list = response.list || [];
        const mediaContainer = document.getElementById('mediaContainer');
        const emptyState = document.getElementById('emptyState');

        if (list.length > 0) {
            if (mediaContainer) mediaContainer.style.display = 'block';
            if (emptyState) emptyState.style.display = 'none';
            this.renderList(list);
        } else {
            if (mediaContainer) mediaContainer.style.display = 'none';
            if (emptyState) emptyState.style.display = 'flex';
        }
    }

    // Helper to determine format badge from filename or stream metadata
    getFormatBadge(text, info) {
        const combined = (text + " " + info).toUpperCase();
        if (combined.includes("M3U8") || combined.includes("HLS")) return "HLS";
        if (combined.includes("MP4")) return "MP4";
        if (combined.includes("WEBM")) return "WEBM";
        if (combined.includes("MKV")) return "MKV";
        if (combined.includes("MP3") || combined.includes("M4A") || combined.includes("AAC")) return "AUDIO";
        if (combined.includes("1080P") || combined.includes("1080")) return "1080P";
        if (combined.includes("720P") || combined.includes("720")) return "720P";
        if (combined.includes("4K") || combined.includes("2160")) return "4K";
        return "VIDEO";
    }

    renderList(arr) {
        const listContainer = document.getElementById("list");
        if (!listContainer) return;
        listContainer.innerHTML = '';

        // Render in reverse order (newest captured stream on top)
        for (let i = arr.length - 1; i >= 0; i--) {
            const listItem = arr[i];
            const text = listItem.text || "Untitled Media";
            const info = listItem.info || "";
            const id = listItem.id;
            const badge = this.getFormatBadge(text, info);

            const card = document.createElement('div');
            card.className = 'media-card';
            card.setAttribute('role', 'button');
            card.setAttribute('tabindex', '0');
            card.setAttribute('title', text);

            const badgeElem = document.createElement('div');
            badgeElem.className = 'media-card-badge';
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

            const actionBtn = document.createElement('button');
            actionBtn.className = 'media-card-action';
            actionBtn.setAttribute('aria-label', `Download ${text}`);
            actionBtn.innerHTML = `
                <svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2.2">
                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>
                </svg>
            `;

            card.appendChild(badgeElem);
            card.appendChild(detailsElem);
            card.appendChild(actionBtn);

            card.addEventListener('click', () => {
                chrome.runtime.sendMessage({ type: "vid", itemId: id });
            });

            listContainer.appendChild(card);
        }
    }
}

const popup = new VideoPopup();
popup.run();
