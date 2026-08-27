// EdgeView machine details — self-refresh (004, client comment 4).
//
// Polls GET /Machines/Data/{id} every 5 s, the same cadence and the same shape as the dashboard.
// Values are written in place, element by element: the script never rewrites a container's
// innerHTML, so scroll position, focus and text selection survive every tick (FR-019). A failed
// poll is swallowed and the last-good values stay on screen (FR-020) — no dialog, no spinner
// replacing content, no redirect if the session has expired.
(function () {
    "use strict";

    var CFG = window.machineLive;
    if (!CFG || !CFG.machineId) return;

    var POLL_MS = 5000;
    // Charts move only at the 30-minute sampling interval, and a rebuilt canvas flickers, so they
    // are refreshed on a far slower cadence than the KPI tiles.
    var CHART_EVERY = 12; // ticks -> 60 s
    var L = CFG.labels || {};

    var tick = 0;

    function num(v) { return (v === null || v === undefined || isNaN(v)) ? 0 : Number(v); }

    function setText(id, value) {
        var el = document.getElementById(id);
        if (el && el.textContent !== value) el.textContent = value;
    }

    function setHidden(id, hidden) {
        var el = document.getElementById(id);
        if (!el) return;
        if (hidden) el.setAttribute("hidden", "hidden");
        else el.removeAttribute("hidden");
    }

    function fixed(v, places) { return num(v).toFixed(places); }

    /** "07:35" from a number of seconds — hours are not wrapped at 24. */
    function hhmm(totalSeconds) {
        var s = Math.max(0, Math.round(num(totalSeconds)));
        var h = Math.floor(s / 3600);
        var m = Math.floor((s % 3600) / 60);
        return (h < 10 ? "0" : "") + h + ":" + (m < 10 ? "0" : "") + m;
    }

    function statusInfo(status) {
        if (status === "Running") return { badge: "status-running", text: L.running || "Running" };
        if (status === "Stopped") return { badge: "status-stopped", text: L.stopped || "Stopped" };
        return { badge: "status-idle", text: L.unknown || "Unknown" };
    }

    function renderStatus(status) {
        var el = document.getElementById("mdStatusBadge");
        if (!el) return;
        var s = statusInfo(status);
        var next = "status-badge " + s.badge + " ms-2";
        if (el.className !== next) el.className = next;
        if (el.textContent !== s.text) el.textContent = s.text;
    }

    function renderBar(barId, valueId, value) {
        var bar = document.getElementById(barId);
        var v = num(value);
        if (bar) bar.style.width = v + "%";
        setText(valueId, v.toFixed(1) + "%");
    }

    function renderTelemetry(data) {
        var oee = data.latest;
        var power = data.power;
        var window24 = data.window || {};

        setText("mdOee", oee ? fixed(oee.oee, 1) : "0.0");
        setText("mdPower", power ? fixed(power.kw, 1) : "0.0");
        setText("mdProduction", fixed(window24.totalProduction, 0));

        var produced = num(window24.totalProduction);
        var quality = produced > 0 ? (num(window24.totalGoods) * 100) / produced : 0;
        setText("mdQualityRate", quality.toFixed(1));

        renderBar("mdBarAvail", "mdBarAvailVal", oee ? oee.availability : 0);
        renderBar("mdBarPerf", "mdBarPerfVal", oee ? oee.performance : 0);
        renderBar("mdBarQual", "mdBarQualVal", oee ? oee.quality : 0);

        setText("mdUptime", hhmm(window24.uptimeSeconds));
        setText("mdDowntime", hhmm(window24.downtimeSeconds));
        setText("mdEnergy", fixed(window24.totalEnergyKwh, 1));

        // The "no data" figure is shown only when it is non-zero (FR-025) — an honest total with no
        // extra noise on a healthy machine.
        var noData = num(window24.noDataSeconds);
        setText("mdNoData", hhmm(noData));
        setHidden("mdNoDataWrap", noData <= 0);

        setText("mdV1", power ? fixed(power.v1, 1) : "0.0");
        setText("mdV2", power ? fixed(power.v2, 1) : "0.0");
        setText("mdV3", power ? fixed(power.v3, 1) : "0.0");
        setText("mdA1", power ? fixed(power.a1, 1) : "0.0");
        setText("mdA2", power ? fixed(power.a2, 1) : "0.0");
        setText("mdA3", power ? fixed(power.a3, 1) : "0.0");
        setText("mdAAvg", power ? fixed(power.aAvg, 1) : "0.0");
        setText("mdFreq", power ? fixed(power.frequency, 2) : "0.00");
    }

    function renderWorkOrder(data) {
        var wo = data.currentWorkOrder;

        setHidden("mdWoContent", !wo);
        setHidden("mdWoEmpty", !!wo);
        setHidden("mdWoOthers", !data.hasOtherWorkOrdersInProgress);

        if (!wo) return;

        setText("mdWoNumber", wo.workOrderNumber || "-");
        setText("mdWoProduct", wo.outputProductName || "-");
        setText("mdWoQty", fixed(wo.qtyToManufacture, 0));
        setText("mdWoInputWeight", fixed(wo.totalInputWeight, 1));
        setText("mdWoElapsed", hhmm(elapsedSeconds(wo)));

        var link = document.getElementById("mdWoLink");
        if (link) {
            var href = (CFG.workOrderUrlBase || "/WorkOrders/Details/") + wo.workOrderId;
            if (link.getAttribute("href") !== href) link.setAttribute("href", href);
        }
    }

    /** The server sends elapsed time as a TimeSpan; accept either shape defensively. */
    function elapsedSeconds(wo) {
        if (typeof wo.elapsedSeconds === "number") return wo.elapsedSeconds;
        if (typeof wo.elapsedTime === "string") {
            var parts = wo.elapsedTime.split(":");
            if (parts.length >= 3) {
                var days = 0;
                var head = parts[0];
                if (head.indexOf(".") > -1) {
                    var split = head.split(".");
                    days = parseInt(split[0], 10) || 0;
                    head = split[1];
                }
                return days * 86400 + (parseInt(head, 10) || 0) * 3600 +
                       (parseInt(parts[1], 10) || 0) * 60 + (parseFloat(parts[2]) || 0);
            }
        }
        return 0;
    }

    function updateCharts(data) {
        var charts = window.mdCharts;
        if (!charts || !data.latest) return;

        // Assign new points and call update() on the existing chart instance — the canvas is never
        // rebuilt, so the series animates instead of flashing.
        var stamp = new Date(data.latest.timestamp);
        var label = ("0" + stamp.getHours()).slice(-2) + ":" + ("0" + stamp.getMinutes()).slice(-2);

        if (charts.oee) {
            var d = charts.oee.data;
            if (d.labels[d.labels.length - 1] !== label) {
                d.labels.push(label);
                d.datasets[0].data.push(num(data.latest.oee));
                d.datasets[1].data.push(num(data.latest.availability));
                d.datasets[2].data.push(num(data.latest.performance));
                d.datasets[3].data.push(num(data.latest.quality));
                while (d.labels.length > 48) {
                    d.labels.shift();
                    d.datasets.forEach(function (ds) { ds.data.shift(); });
                }
                charts.oee.update();
            }
        }

        if (charts.power && data.power) {
            var p = charts.power.data;
            if (p.labels[p.labels.length - 1] !== label) {
                p.labels.push(label);
                p.datasets[0].data.push(num(data.power.kw));
                p.datasets[1].data.push((num(data.power.v1) + num(data.power.v2) + num(data.power.v3)) / 3);
                while (p.labels.length > 48) {
                    p.labels.shift();
                    p.datasets.forEach(function (ds) { ds.data.shift(); });
                }
                charts.power.update();
            }
        }
    }

    function render(data) {
        // The page was rendered before this machine had ever reported, so the KPI markup does not
        // exist to update. One reload builds it; after that every refresh is in place.
        if (CFG.renderedEmpty && (data.latest || data.power)) {
            window.location.reload();
            return;
        }

        renderStatus(data.status);
        renderWorkOrder(data);
        if (!CFG.renderedEmpty) {
            renderTelemetry(data);
            if (tick % CHART_EVERY === 0) updateCharts(data);
        }

        var stamp = document.getElementById("mdLastUpdated");
        if (stamp && data.generatedAt) {
            stamp.textContent = (L.lastUpdated || "Last updated") + ": " +
                new Date(data.generatedAt).toLocaleTimeString();
        }
    }

    function poll() {
        fetch("/Machines/Data/" + CFG.machineId, { headers: { "Accept": "application/json" } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) {
                if (!data) return; // 404, or a 302 to the login page — keep the last-good view
                render(data);
                tick++;
            })
            .catch(function () { /* transient failure: keep what is on screen and retry next tick */ });
    }

    poll();
    setInterval(poll, POLL_MS);
})();
