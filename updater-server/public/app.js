/* ==========================================================================
   Universe Interactive Web App & Micro-Animations Engine
   ========================================================================== */

document.addEventListener('DOMContentLoaded', () => {
    initParticleStarfield();
    initSpotlightCursor();
    init3DTilt();
    initScrollReveal();
    initInteractiveFenceDemo();
    fetchLiveVersion();
});

/* ── 1. Floating Particle Starfield Engine ───────────────────────────────── */
function initParticleStarfield() {
    const canvas = document.getElementById('particleCanvas');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');

    let width = canvas.width = window.innerWidth;
    let height = canvas.height = window.innerHeight;

    window.addEventListener('resize', () => {
        width = canvas.width = window.innerWidth;
        height = canvas.height = window.innerHeight;
    });

    const particles = [];
    const count = Math.min(65, Math.floor(window.innerWidth / 20));

    const colors = [
        'rgba(0, 245, 212, 0.45)',  // Cyan
        'rgba(139, 92, 246, 0.45)', // Violet
        'rgba(244, 63, 94, 0.35)',  // Pink
        'rgba(255, 255, 255, 0.25)' // White
    ];

    for (let i = 0; i < count; i++) {
        particles.push({
            x: Math.random() * width,
            y: Math.random() * height,
            radius: Math.random() * 1.8 + 0.5,
            color: colors[Math.floor(Math.random() * colors.length)],
            vx: (Math.random() - 0.5) * 0.4,
            vy: (Math.random() - 0.5) * 0.4 - 0.15,
            pulse: Math.random() * Math.PI
        });
    }

    let mouseX = -1000, mouseY = -1000;
    window.addEventListener('mousemove', (e) => {
        mouseX = e.clientX;
        mouseY = e.clientY;
    });

    function render() {
        ctx.clearRect(0, 0, width, height);

        for (let p of particles) {
            p.x += p.vx;
            p.y += p.vy;
            p.pulse += 0.02;

            if (p.x < 0) p.x = width;
            if (p.x > width) p.x = 0;
            if (p.y < 0) p.y = height;
            if (p.y > height) p.y = 0;

            // Subtle mouse repulsion
            const dx = mouseX - p.x;
            const dy = mouseY - p.y;
            const dist = Math.sqrt(dx * dx + dy * dy);
            if (dist < 120) {
                p.x -= (dx / dist) * 0.8;
                p.y -= (dy / dist) * 0.8;
            }

            const currentRadius = p.radius + Math.sin(p.pulse) * 0.4;

            ctx.beginPath();
            ctx.arc(p.x, p.y, Math.max(0.2, currentRadius), 0, Math.PI * 2);
            ctx.fillStyle = p.color;
            ctx.fill();
        }

        requestAnimationFrame(render);
    }

    render();
}

/* ── 2. Interactive Spotlight Cursor Glow (Linear / Raycast Style) ─────── */
function initSpotlightCursor() {
    const cards = document.querySelectorAll('.spotlight-card');
    
    document.addEventListener('mousemove', (e) => {
        cards.forEach(card => {
            const rect = card.getBoundingClientRect();
            const x = e.clientX - rect.left;
            const y = e.clientY - rect.top;

            card.style.setProperty('--mouse-x', `${x}px`);
            card.style.setProperty('--mouse-y', `${y}px`);
        });
    });
}

/* ── 3. 3D Perspective Tilt on Interactive Demo ────────────────────────── */
function init3DTilt() {
    const container = document.getElementById('tiltContainer');
    const fence = document.getElementById('mockFence');
    if (!container || !fence) return;

    container.addEventListener('mousemove', (e) => {
        const rect = container.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const y = e.clientY - rect.top;

        const centerX = rect.width / 2;
        const centerY = rect.height / 2;

        const rotateX = ((y - centerY) / centerY) * -7; // max -7 deg to 7 deg
        const rotateY = ((x - centerX) / centerX) * 7;

        fence.style.transform = `perspective(1000px) rotateX(${rotateX.toFixed(2)}deg) rotateY(${rotateY.toFixed(2)}deg) scale3d(1.015, 1.015, 1.015)`;
    });

    container.addEventListener('mouseleave', () => {
        fence.style.transform = 'perspective(1000px) rotateX(0deg) rotateY(0deg) scale3d(1, 1, 1)';
    });
}

/* ── 4. Scroll Reveal & Number Counter Observer ────────────────────────── */
function initScrollReveal() {
    const elements = document.querySelectorAll('.reveal-on-scroll');

    const observer = new IntersectionObserver((entries, obs) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('revealed');
                obs.unobserve(entry.target);
            }
        });
    }, {
        threshold: 0.12,
        rootMargin: '0px 0px -40px 0px'
    });

    elements.forEach(el => observer.observe(el));
}

/* ── 5. Interactive Fence Simulator ────────────────────────────────────── */
function initInteractiveFenceDemo() {
    const grid = document.getElementById('fenceGrid');
    const items = grid ? grid.querySelectorAll('.bubble-item') : [];
    const searchBtn = document.getElementById('btnToggleSearch');
    const searchBar = document.getElementById('searchBar');
    const searchInput = document.getElementById('demoSearchInput');
    const clearBtn = document.getElementById('btnClearSearch');
    const badge = document.getElementById('fenceBadge');
    const themeChips = document.querySelectorAll('.btn-theme-chip');
    const mockFence = document.getElementById('mockFence');

    // Click selection
    items.forEach(item => {
        item.addEventListener('click', () => {
            items.forEach(i => i.classList.remove('selected'));
            item.classList.add('selected');
        });
    });

    // Toggle search bar
    if (searchBtn && searchBar) {
        searchBtn.addEventListener('click', () => {
            searchBar.classList.toggle('active');
            if (searchBar.classList.contains('active')) {
                searchInput.focus();
            } else {
                searchInput.value = '';
                filterItems('');
            }
        });
    }

    if (clearBtn) {
        clearBtn.addEventListener('click', () => {
            searchInput.value = '';
            filterItems('');
            searchInput.focus();
        });
    }

    if (searchInput) {
        searchInput.addEventListener('input', (e) => {
            filterItems(e.target.value.toLowerCase());
        });
    }

    function filterItems(query) {
        let visibleCount = 0;
        items.forEach(item => {
            const name = (item.dataset.name || item.textContent).toLowerCase();
            if (name.includes(query)) {
                item.style.display = 'block';
                visibleCount++;
            } else {
                item.style.display = 'none';
            }
        });

        if (badge) {
            badge.textContent = `${visibleCount} item${visibleCount === 1 ? '' : 's'}`;
        }
    }

    // Theme switching in demo
    themeChips.forEach(chip => {
        chip.addEventListener('click', () => {
            themeChips.forEach(c => c.classList.remove('active'));
            chip.classList.add('active');

            const theme = chip.dataset.theme;
            if (theme === 'obsidian') {
                mockFence.style.background = 'rgba(10, 12, 16, 0.95)';
                mockFence.style.borderColor = 'rgba(255, 255, 255, 0.12)';
                mockFence.style.boxShadow = '0 20px 40px rgba(0,0,0,0.8)';
            } else if (theme === 'frost') {
                mockFence.style.background = 'rgba(255, 255, 255, 0.08)';
                mockFence.style.borderColor = 'rgba(255, 255, 255, 0.25)';
                mockFence.style.boxShadow = '0 20px 50px rgba(0,0,0,0.4)';
            } else {
                mockFence.style.background = 'rgba(13, 16, 24, 0.88)';
                mockFence.style.borderColor = 'rgba(139, 92, 246, 0.35)';
                mockFence.style.boxShadow = '0 20px 50px rgba(0, 0, 0, 0.6), 0 0 30px rgba(139, 92, 246, 0.15)';
            }
        });
    });
}

/* ── 6. Dynamic Version Check from Manifest ────────────────────────────── */
async function fetchLiveVersion() {
    try {
        const res = await fetch('/version.json');
        if (!res.ok) return;
        const data = await res.json();
        if (data.version) {
            const versionPills = document.querySelectorAll('.badge-pill');
            versionPills.forEach(p => p.textContent = `v${data.version}`);
        }
    } catch (e) {
        // Fallback gracefully
    }
}
