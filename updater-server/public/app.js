// ── Universe Landing Page Interactive Controller ────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
    // 1. Live Interactive Fence Controller
    const mockFence = document.getElementById('mockFence');
    const searchBar = document.getElementById('searchBar');
    const btnToggleSearch = document.getElementById('btnToggleSearch');
    const demoSearchInput = document.getElementById('demoSearchInput');
    const btnClearSearch = document.getElementById('btnClearSearch');
    const fenceBadge = document.getElementById('fenceBadge');
    const bubbleItems = document.querySelectorAll('.bubble-item');
    const themeChips = document.querySelectorAll('.btn-theme-chip');

    // Toggle Search Bar
    function toggleSearch() {
        searchBar.classList.toggle('active');
        if (searchBar.classList.contains('active')) {
            demoSearchInput.focus();
        } else {
            demoSearchInput.value = '';
            filterItems('');
        }
    }

    btnToggleSearch?.addEventListener('click', toggleSearch);
    
    // Quick-Search Hotkey Ctrl+F inside the demo
    window.addEventListener('keydown', (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'f') {
            e.preventDefault();
            searchBar.classList.add('active');
            demoSearchInput.focus();
        }
    });

    // Real-time filtering
    demoSearchInput?.addEventListener('input', (e) => {
        filterItems(e.target.value);
    });

    btnClearSearch?.addEventListener('click', () => {
        demoSearchInput.value = '';
        filterItems('');
        searchBar.classList.remove('active');
    });

    function filterItems(query) {
        const q = query.trim().toLowerCase();
        let visibleCount = 0;

        bubbleItems.forEach(item => {
            const name = (item.getAttribute('data-name') || '').toLowerCase();
            if (!q || name.includes(q)) {
                item.style.display = 'block';
                visibleCount++;
            } else {
                item.style.display = 'none';
            }
        });

        if (fenceBadge) {
            fenceBadge.textContent = `${visibleCount} item${visibleCount === 1 ? '' : 's'}`;
        }
    }

    // Interactive Bubble Selection & Kinetic Pulse
    bubbleItems.forEach(item => {
        item.addEventListener('click', () => {
            bubbleItems.forEach(b => b.classList.remove('selected'));
            item.classList.add('selected');

            // Trigger breathing pulse effect on parent fence
            mockFence.style.transform = 'scale(1.015)';
            setTimeout(() => {
                mockFence.style.transform = 'scale(1)';
            }, 200);
        });
    });

    // Theme Switcher Chips
    themeChips.forEach(chip => {
        chip.addEventListener('click', () => {
            themeChips.forEach(c => c.classList.remove('active'));
            chip.classList.add('active');

            const theme = chip.getAttribute('data-theme');
            mockFence.classList.remove('theme-obsidian', 'theme-frost');

            if (theme === 'obsidian') {
                mockFence.classList.add('theme-obsidian');
            } else if (theme === 'frost') {
                mockFence.classList.add('theme-frost');
            }
        });
    });

    // 2. Fetch Live Version Manifest and update Website Dynamically
    fetch('/version.json')
        .then(res => res.json())
        .then(data => {
            if (data && data.version) {
                // Update all version badges
                const badges = document.querySelectorAll('.badge-pill');
                badges.forEach(b => b.textContent = `v${data.version}`);

                // Update Hero Download Button
                const downloadBtn = document.getElementById('downloadBtn');
                if (downloadBtn) {
                    const title = downloadBtn.querySelector('strong');
                    if (title) title.textContent = `Download Universe v${data.version}`;
                    if (data.downloadUrl) downloadBtn.href = data.downloadUrl;
                }

                // Update any other download links
                const allDownloadLinks = document.querySelectorAll('a[href*="Universe_Setup"]');
                allDownloadLinks.forEach(link => {
                    if (data.downloadUrl) link.href = data.downloadUrl;
                });
            }
        })
        .catch(() => {
            // Silently fallback to static defaults
        });
});
