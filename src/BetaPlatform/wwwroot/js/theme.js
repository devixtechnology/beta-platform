/* Beta Platform — light/dark theme toggle (feature 002-ui-modernization).
   The initial theme is rendered server-side in _Layout from the `beta_theme` cookie
   (no flash). This script only handles user toggles: flip the attribute instantly
   (no reload) and persist the choice back to the cookie. */
(function () {
    'use strict';

    var COOKIE = 'beta_theme';
    var root = document.documentElement;

    function writeCookie(value) {
        var oneYear = 60 * 60 * 24 * 365;
        document.cookie = COOKIE + '=' + value + ';path=/;max-age=' + oneYear + ';SameSite=Lax';
    }

    function currentTheme() {
        return root.getAttribute('data-bs-theme') === 'light' ? 'light' : 'dark';
    }

    function apply(theme, persist) {
        root.setAttribute('data-bs-theme', theme);
        if (persist) { writeCookie(theme); }
        document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
            btn.setAttribute('aria-pressed', theme === 'light' ? 'true' : 'false');
        });
    }

    function toggle() {
        apply(currentTheme() === 'light' ? 'dark' : 'light', true);
    }

    function bind() {
        document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
            btn.addEventListener('click', function (e) { e.preventDefault(); toggle(); });
            btn.setAttribute('aria-pressed', currentTheme() === 'light' ? 'true' : 'false');
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bind);
    } else {
        bind();
    }

    window.BetaTheme = { toggle: toggle, apply: apply, current: currentTheme };
})();
