// PropLink Interactive Scripts & Dark Mode Controller

document.addEventListener('DOMContentLoaded', () => {
    // =========================================================================
    // 1. Theme Management (Dark Mode & Light Mode)
    // =========================================================================
    const getStoredTheme = () => localStorage.getItem('proplink-theme');
    const setStoredTheme = theme => localStorage.setItem('proplink-theme', theme);

    const getPreferredTheme = () => {
        const storedTheme = getStoredTheme();
        if (storedTheme) {
            return storedTheme;
        }
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    };

    const updateThemeIcons = (theme) => {
        const moonIcons = document.querySelectorAll('.theme-icon-moon');
        const sunIcons = document.querySelectorAll('.theme-icon-sun');

        if (theme === 'dark') {
            moonIcons.forEach(el => el.classList.add('d-none'));
            sunIcons.forEach(el => el.classList.remove('d-none'));
        } else {
            moonIcons.forEach(el => el.classList.remove('d-none'));
            sunIcons.forEach(el => el.classList.add('d-none'));
        }
    };

    const setTheme = (theme) => {
        document.documentElement.setAttribute('data-theme', theme);
        document.documentElement.setAttribute('data-bs-theme', theme);
        setStoredTheme(theme);
        updateThemeIcons(theme);
    };

    // Initialize current theme state
    const currentTheme = getPreferredTheme();
    setTheme(currentTheme);

    // Bind theme toggle buttons
    const themeToggleBtn = document.getElementById('themeToggleBtn');
    const themeToggleBtnMobile = document.getElementById('themeToggleBtnMobile');

    const handleThemeToggle = () => {
        const activeTheme = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        setTheme(activeTheme);
    };

    if (themeToggleBtn) {
        themeToggleBtn.addEventListener('click', handleThemeToggle);
    }
    if (themeToggleBtnMobile) {
        themeToggleBtnMobile.addEventListener('click', handleThemeToggle);
    }

    // Listen for OS system theme changes
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
        const storedTheme = getStoredTheme();
        if (!storedTheme) {
            setTheme(getPreferredTheme());
        }
    });

    // =========================================================================
    // 2. Search Tab Buttons (Buy / Rent / Commercial)
    // =========================================================================
    const searchTabs = document.querySelectorAll('.search-tabs .tab-btn');
    searchTabs.forEach(tab => {
        tab.addEventListener('click', () => {
            searchTabs.forEach(t => t.classList.remove('active'));
            tab.classList.add('active');
        });
    });

    // =========================================================================
    // 3. Category Filter Pills (Catalog / Listings)
    // =========================================================================
    const filterPills = document.querySelectorAll('.filter-pill');
    const propertyItems = document.querySelectorAll('.property-item');

    filterPills.forEach(pill => {
        pill.addEventListener('click', () => {
            filterPills.forEach(p => p.classList.remove('active'));
            pill.classList.add('active');

            const filter = pill.getAttribute('data-filter');

            propertyItems.forEach(item => {
                const itemType = item.getAttribute('data-type');
                if (filter === 'all' || itemType === filter) {
                    item.style.display = '';
                    item.classList.add('fade-in');
                } else {
                    item.style.display = 'none';
                }
            });
        });
    });

    // =========================================================================
    // 4. Smooth Scrolling for Internal Anchor Links
    // =========================================================================
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const targetId = this.getAttribute('href');
            if (targetId && targetId !== '#') {
                const targetElement = document.querySelector(targetId);
                if (targetElement) {
                    e.preventDefault();
                    targetElement.scrollIntoView({
                        behavior: 'smooth',
                        block: 'start'
                    });
                }
            }
        });
    });
});
