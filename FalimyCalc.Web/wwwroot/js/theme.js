// Theme management: toggles and persists light/dark mode
(function () {
    const STORAGE_KEY = 'falimycalc_theme';

    function getPreferredTheme() {
        const saved = localStorage.getItem(STORAGE_KEY);
        if (saved === 'light' || saved === 'dark') {
            return saved;
        }
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }

    function applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        document.documentElement.style.colorScheme = theme;
        localStorage.setItem(STORAGE_KEY, theme);
        updateButtonUi(theme);
    }

    function updateButtonUi(theme) {
        const btn = document.getElementById('theme-toggle-btn');
        if (!btn) return;

        const icon = btn.querySelector('.theme-icon');
        const text = btn.querySelector('.theme-text');

        if (theme === 'dark') {
            if (icon) icon.textContent = '☀️';
            if (text) text.textContent = 'Light Mode';
            btn.setAttribute('aria-label', 'Switch to light mode');
            btn.setAttribute('title', 'Switch to light mode');
        } else {
            if (icon) icon.textContent = '🌙';
            if (text) text.textContent = 'Dark Mode';
            btn.setAttribute('aria-label', 'Switch to dark mode');
            btn.setAttribute('title', 'Switch to dark mode');
        }
    }

    window.toggleAppTheme = function () {
        const current = document.documentElement.getAttribute('data-theme') || getPreferredTheme();
        const next = current === 'dark' ? 'light' : 'dark';
        applyTheme(next);
    };

    window.initAppTheme = function () {
        const theme = getPreferredTheme();
        applyTheme(theme);
    };

    // Apply immediately to prevent flash
    const initialTheme = getPreferredTheme();
    document.documentElement.setAttribute('data-theme', initialTheme);
    document.documentElement.style.colorScheme = initialTheme;

    // Hook DOMContentLoaded and MutationObserver to ensure button UI updates
    document.addEventListener('DOMContentLoaded', function () {
        updateButtonUi(document.documentElement.getAttribute('data-theme') || initialTheme);
    });
})();
