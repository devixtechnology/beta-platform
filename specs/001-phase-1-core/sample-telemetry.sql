-- Dev-only sample telemetry for validating the monitoring dashboard (US4 / quickstart.md).
-- This mimics what the IoT team writes directly into oee_data / power_data in production.
-- The Beta application itself NEVER writes these tables (FR-042); this script is a manual
-- validation aid only and is not part of the application.
--
-- Prerequisite: create machines first via the UI (Machines → Add), then note their machine_id
-- values (SELECT machine_id, machine_code FROM machines;) and substitute below.

-- === Machine 1: running, healthy OEE ===
INSERT INTO oee_data (machine_id, `timestamp`, availability, quality, performance, total_count, total_goods, status, created_at, order_id)
VALUES (1, NOW(6), 95.00, 96.50, 90.00, 1200, 1160, 1, NOW(6), NULL);

INSERT INTO power_data (machine_id, `timestamp`, kw_hr, v1, v2, v3, v12, v23, v13, a1, a2, a3, a_avg, frequency, created_at)
VALUES (1, NOW(3), 12.40, 231.0, 230.5, 229.8, 400.1, 399.7, 400.3, 18.2, 18.0, 18.4, 18.2, 50.00, NOW(6));

-- === Machine 2: stopped ===
INSERT INTO oee_data (machine_id, `timestamp`, availability, quality, performance, total_count, total_goods, status, created_at, order_id)
VALUES (2, NOW(6), 0.00, 0.00, 0.00, 0, 0, 0, NOW(6), NULL);

INSERT INTO power_data (machine_id, `timestamp`, kw_hr, frequency, created_at)
VALUES (2, NOW(3), 0.20, 50.00, NOW(6));

-- Machine 3 (if present) is intentionally left WITHOUT telemetry to verify the dashboard's
-- graceful "Unknown / No telemetry" state (FR-044, SC-005).

-- To simulate the ~5s live refresh: run an updated INSERT for a machine, then watch the
-- dashboard card change within one poll cycle.
-- INSERT INTO oee_data (machine_id, `timestamp`, availability, quality, performance, total_count, total_goods, status, created_at)
-- VALUES (1, NOW(6), 88.00, 94.00, 85.00, 1350, 1290, 1, NOW(6));
