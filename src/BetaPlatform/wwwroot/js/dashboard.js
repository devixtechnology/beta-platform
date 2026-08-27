// EdgeView dashboard — periodic polling (~5s). No SignalR (FR-043).
(function () {
    "use strict";
    var POLL_MS = 5000;
    var L = window.betaLabels || {};

    function esc(v) { return (v === null || v === undefined) ? "" : String(v); }
    function num(v) { return (v === null || v === undefined || isNaN(v)) ? 0 : Number(v); }

    // OEE health thresholds — mirrors GaugeCol() in the Razor views.
    function gaugeCol(v) { return v >= 85 ? "var(--success)" : (v >= 50 ? "var(--warning)" : "var(--danger)"); }

    function statusInfo(status) {
        if (status === "Running") return { color: "success", badge: "status-running", text: L.running || "Running", live: true };
        if (status === "Stopped") return { color: "danger", badge: "status-stopped", text: L.stopped || "Stopped", live: false };
        return { color: "secondary", badge: "status-idle", text: L.unknown || "Unknown", live: false };
    }

    function barRow(label, value, color) {
        var v = num(value);
        return '<div class="mc-bar-row">' +
                   '<span class="b-lab">' + esc(label) + '</span>' +
                   '<div class="progress"><div class="progress-bar bg-' + color + '" style="width:' + v + '%"></div></div>' +
                   '<span class="b-val">' + v.toFixed(0) + '%</span>' +
               '</div>';
    }

    function gaugeHtml(oee, extraClass, capLabel) {
        var v = num(oee);
        var cap = capLabel ? '<span class="gauge-cap">' + esc(capLabel) + '</span>' : '';
        return '<div class="gauge ' + (extraClass || "") + '" style="--val:' + v + '; --col:' + gaugeCol(v) + '">' +
                   '<div class="gauge-hole"><span class="gauge-num">' + v.toFixed(0) + '<small>%</small>' + cap + '</span></div>' +
               '</div>';
    }

    function cardHtml(m) {
        var s = statusInfo(m.status);
        var body;
        if (m.hasTelemetry && m.oee) {
            var kw = (m.power && m.power.kw !== null && m.power.kw !== undefined) ? num(m.power.kw).toFixed(1) : "0.0";
            body =
                '<div class="mc-main">' +
                    gaugeHtml(m.oee.value, "", L.oee) +
                    '<div class="mc-stats">' +
                        '<div class="mc-stat"><span class="s-val">' + kw + ' <small class="text-muted">kW</small></span><span class="s-lab">' + esc(L.power) + '</span></div>' +
                        '<div class="mc-stat"><span class="s-val">' + num(m.oee.totalWeight).toFixed(0) + '</span><span class="s-lab">' + esc(L.totalWeight) + '</span></div>' +
                        '<div class="mc-stat"><span class="s-val">' + num(m.oee.totalCount).toFixed(0) + '</span><span class="s-lab">' + esc(L.totalCount) + '</span></div>' +
                        // Input Weight replaces Good Units, matching _MachineCard.cshtml so the polled
                        // card and the server-rendered card stay identical (004, client comment 7).
                        '<div class="mc-stat"><span class="s-val">' + num(m.inputWeight).toFixed(1) + '</span><span class="s-lab">' + esc(L.inputWeight) + '</span></div>' +
                    '</div>' +
                '</div>' +
                '<div class="mc-bars">' +
                    barRow(L.availability, m.oee.availability, "success") +
                    barRow(L.performance, m.oee.performance, "info") +
                    barRow(L.quality, m.oee.quality, "warning") +
                '</div>';
        } else {
            body = '<div class="text-center text-muted py-4"><i class="bi bi-wifi-off fs-3"></i><div class="small mt-2">' + esc(L.noTelemetry) + '</div></div>';
        }
        var detailsBtn = '<a class="btn btn-sm btn-outline-primary w-100" href="/Machines/Details/' + esc(m.machineId) +
            '"><i class="bi bi-eye me-1"></i>' + esc(L.details || "Details") + '</a>';
        return '<div class="col-xl-4 col-md-6">' +
            '<div class="card machine-card status-' + s.color + ' h-100"><div class="card-body">' +
                '<div class="mc-head">' +
                    '<div class="min-w-0"><h5 class="mc-name text-truncate">' + esc(m.machineName) + '</h5><p class="mc-code">' + esc(m.machineCode) + '</p></div>' +
                    '<span class="status-badge ' + s.badge + (s.live ? ' mc-live' : '') + '">' + esc(s.text) + '</span>' +
                '</div>' + body + detailsBtn +
            '</div></div></div>';
    }

    function setText(id, value) {
        var el = document.getElementById(id);
        if (el) el.textContent = value;
    }

    function renderSummary(sum, running, total) {
        if (!sum) return;
        var avg = num(sum.averageOee);
        // Featured hero gauge
        var gauge = document.getElementById("heroGauge");
        if (gauge) {
            gauge.style.setProperty("--val", avg);
            gauge.style.setProperty("--col", gaugeCol(avg));
        }
        setText("heroGaugeVal", avg.toFixed(1));
        setText("heroRunning", running + " / " + total);
        var pulse = document.getElementById("heroPulse");
        if (pulse) pulse.className = "pulse-dot" + (running > 0 ? " running" : "");

        // KPI tiles
        setText("sumEnergy", num(sum.totalEnergyKwh).toFixed(1));
        setText("sumUnits", num(sum.unitsProduced).toFixed(0));
        setText("sumFinishedWos", esc(sum.finishedWorkOrders));
        setText("sumAvailability", num(sum.availability).toFixed(1) + "%");
        setText("sumPerformance", num(sum.performance).toFixed(1) + "%");
        setText("sumQuality", num(sum.quality).toFixed(1) + "%");
    }

    function render(data) {
        var grid = document.getElementById("machineGrid");
        if (!grid) return;
        var machines = data.machines || [];
        if (machines.length) {
            grid.innerHTML = machines.map(cardHtml).join("");
        }

        var running = machines.filter(function (m) { return m.status === "Running"; }).length;
        var count = document.getElementById("runningCount");
        if (count) count.textContent = running + " / " + machines.length + " " + (L.runningWord || "Running");

        renderSummary(data.summary, running, machines.length);

        var stamp = document.getElementById("lastUpdated");
        if (stamp && data.generatedAt) {
            stamp.textContent = (L.lastUpdated || "Last updated") + ": " + new Date(data.generatedAt).toLocaleTimeString();
        }
    }

    function poll() {
        fetch("/Dashboard/Data", { headers: { "Accept": "application/json" } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) { if (data) render(data); })
            .catch(function () { /* keep last-good view on transient errors */ });
    }

    poll();
    setInterval(poll, POLL_MS);
})();
