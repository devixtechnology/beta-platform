// EdgeView production-floor display (004, client comment 6).
//
// Polls the SAME /Dashboard/Data endpoint the dashboard uses, so the wall can never disagree with
// the screen on someone's desk. Up to 6 machines fill one 3x2 page; beyond that the view paginates
// and rotates every 15 s with a page indicator, so a viewer knows more exists.
//
// It runs unattended for a full shift: a failed poll — including a 302 to the login page when a
// session lapses — is treated as "no new data", never as a reason to blank the screen or navigate
// away (FR-039).
(function () {
    "use strict";

    var CFG = window.displayConfig || {};
    var L = CFG.labels || {};

    var POLL_MS = 5000;
    var ROTATE_MS = 15000;
    var PAGE_SIZE = 6;

    var machines = [];
    var pageIndex = 0;
    var refreshing = false;

    var grid = document.getElementById("displayGrid");
    var empty = document.getElementById("displayEmpty");
    var pageWrap = document.getElementById("displayPage");
    var pageNum = document.getElementById("displayPageNum");
    var stamp = document.getElementById("displayStamp");

    if (!grid) return;

    function num(v) { return (v === null || v === undefined || isNaN(v)) ? 0 : Number(v); }
    function esc(v) {
        return String(v === null || v === undefined ? "" : v)
            .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;").replace(/'/g, "&#39;");
    }
    function gaugeCol(v) { return v >= 85 ? "var(--success)" : (v >= 50 ? "var(--warning)" : "var(--danger)"); }

    function statusInfo(status) {
        if (status === "Running") return { badge: "status-running", text: L.running || "Running" };
        if (status === "Stopped") return { badge: "status-stopped", text: L.stopped || "Stopped" };
        return { badge: "status-idle", text: L.unknown || "Unknown" };
    }

    /** A machine that is not reporting shows a dash, never a confident zero. */
    function statOrDash(hasValue, value, places) {
        return hasValue ? num(value).toFixed(places) : "—";
    }

    function barRow(label, value, color) {
        var v = num(value);
        return '<div class="display-bar-row">' +
                   '<span class="d-blab">' + esc(label) + '</span>' +
                   '<div class="progress"><div class="progress-bar bg-' + color + '" style="width:' + v + '%"></div></div>' +
                   '<span class="d-bval">' + v.toFixed(0) + '%</span>' +
               '</div>';
    }

    function tileHtml(m) {
        var s = statusInfo(m.status);
        var oee = m.oee ? num(m.oee.value) : 0;
        var hasPower = !!(m.power && m.power.kw !== null && m.power.kw !== undefined);

        return '<div class="display-tile">' +
            '<div class="display-tile-head">' +
                '<div class="min-w-0">' +
                    '<p class="display-name">' + esc(m.machineName) + '</p>' +
                    '<p class="display-code">' + esc(m.machineCode) + '</p>' +
                '</div>' +
                '<span class="status-badge display-status ' + s.badge + '">' + esc(s.text) + '</span>' +
            '</div>' +
            '<div class="display-tile-main">' +
                '<div class="gauge display-gauge" style="--val:' + oee + '; --col:' + gaugeCol(oee) + '">' +
                    '<div class="gauge-hole"><span class="gauge-num">' + oee.toFixed(0) +
                        '<small>%</small><span class="gauge-cap">' + esc(L.oee || "OEE") + '</span></span></div>' +
                '</div>' +
                '<div class="display-stats">' +
                    '<div class="display-stat"><span class="d-val">' + statOrDash(hasPower, m.power && m.power.kw, 1) +
                        '</span><span class="d-lab">' + esc(L.power) + ' kW</span></div>' +
                    '<div class="display-stat"><span class="d-val">' + statOrDash(!!m.oee, m.oee && m.oee.totalWeight, 0) +
                        '</span><span class="d-lab">' + esc(L.totalWeight) + '</span></div>' +
                    '<div class="display-stat"><span class="d-val">' + statOrDash(!!m.oee, m.oee && m.oee.totalCount, 0) +
                        '</span><span class="d-lab">' + esc(L.totalCount) + '</span></div>' +
                    '<div class="display-stat"><span class="d-val">' + num(m.inputWeight).toFixed(1) +
                        '</span><span class="d-lab">' + esc(L.inputWeight) + '</span></div>' +
                '</div>' +
            '</div>' +
            '<div class="display-bars">' +
                barRow(L.availability, m.oee && m.oee.availability, "success") +
                barRow(L.performance, m.oee && m.oee.performance, "info") +
                barRow(L.quality, m.oee && m.oee.quality, "warning") +
            '</div>' +
        '</div>';
    }

    function pageCount() {
        return Math.max(1, Math.ceil(machines.length / PAGE_SIZE));
    }

    function renderPage() {
        if (!machines.length) {
            grid.innerHTML = "";
            grid.setAttribute("hidden", "hidden");
            if (empty) empty.removeAttribute("hidden");
            if (pageWrap) pageWrap.setAttribute("hidden", "hidden");
            return;
        }

        grid.removeAttribute("hidden");
        if (empty) empty.setAttribute("hidden", "hidden");

        var pages = pageCount();
        if (pageIndex >= pages) pageIndex = 0;

        var start = pageIndex * PAGE_SIZE;
        var slice = machines.slice(start, start + PAGE_SIZE);
        grid.innerHTML = slice.map(tileHtml).join("");

        // A single machine is centred on its own, with nothing to rotate to.
        if (machines.length === 1) grid.classList.add("display-grid-single");
        else grid.classList.remove("display-grid-single");

        if (pageWrap) {
            if (pages > 1) {
                pageWrap.removeAttribute("hidden");
                if (pageNum) pageNum.textContent = (pageIndex + 1) + " / " + pages;
            } else {
                pageWrap.setAttribute("hidden", "hidden");
            }
        }
    }

    function rotate() {
        // Never turn a page while a refresh is landing — a tile must not change underneath the
        // transition.
        if (refreshing) return;
        var pages = pageCount();
        if (pages <= 1) return;

        grid.classList.add("is-rotating");
        window.setTimeout(function () {
            pageIndex = (pageIndex + 1) % pages;
            renderPage();
            grid.classList.remove("is-rotating");
        }, 220);
    }

    function apply(data) {
        refreshing = true;

        var next = data.machines || [];
        var previousPages = pageCount();
        machines = next;

        // Recalculate pages when the machine list changes: keep the current page if it is still
        // valid, otherwise fall back to the first.
        if (previousPages !== pageCount() && pageIndex >= pageCount()) pageIndex = 0;

        renderPage();

        if (stamp && data.generatedAt) {
            stamp.textContent = (L.lastUpdated || "Last updated") + ": " +
                new Date(data.generatedAt).toLocaleTimeString();
        }

        refreshing = false;
    }

    function poll() {
        fetch("/Dashboard/Data", { headers: { "Accept": "application/json" } })
            .then(function (r) {
                // A 302 to the login page arrives here as an HTML response, not JSON. Treat it as a
                // failed poll: the wall keeps its last-good render rather than showing a login form.
                if (!r.ok) return null;
                var type = r.headers.get("content-type") || "";
                if (type.indexOf("json") === -1) return null;
                return r.json();
            })
            .then(function (data) { if (data) apply(data); })
            .catch(function () { /* keep the last-good render and retry on the next tick */ });
    }

    poll();
    window.setInterval(poll, POLL_MS);
    window.setInterval(rotate, ROTATE_MS);
})();
