-- =============================================================================
-- Beta Platform - 5 production machines
-- Generated 2026-09-15.
--
-- Seeds five machines across the two production lines that migration
-- 20260707212432_InitialCreate already creates in `machine_types`:
--   machine_type_id 1  'Forming Machine'   line 'Armor Rod & Guy Grip line'
--   machine_type_id 2  'Flat Washer Line'  line 'Flat Washer Line'
--
-- The type ids are resolved BY NAME below rather than hard-coded, so the script
-- still does the right thing if a target database numbered them differently.
--
-- COLUMN NOTES
--   machine_name  UNIQUE, max 50. Must not collide with an existing machine.
--   machine_code  UNIQUE, max 20.
--   is_running    0 for every row. This flag is driven by telemetry/work-order
--                 state at runtime; seeding it as 1 would show machines running
--                 on a dashboard that has no oee_data behind them.
--   machine_id    left to AUTO_INCREMENT. SEE NOTE 2 - read it before running
--                 this on staging if the IoT team is already sending telemetry.
--
-- Not seeded: machines_master_data, machines_kpis, machine_properties,
-- machine_tags, sources_available. Those are IoT tag-configuration tables,
-- empty on the current local database, and a machine works without them.
--
-- Re-runnable: ON DUPLICATE KEY UPDATE. Re-running refreshes the type/active
-- flags and never duplicates a row. SEE NOTE 1.
--
-- Usage:
--   mysql --default-character-set=utf8mb4 -u <user> -p <database> < seed-machines.sql
-- =============================================================================

SET NAMES utf8mb4;

-- Resolve the two production lines by name. If either comes back NULL the
-- INSERT below fails on the NOT NULL / foreign-key constraint rather than
-- silently writing a bad machine_type_id - run the EF migrations first.
SET @forming := (SELECT machine_type_id FROM machine_types WHERE name = 'Forming Machine');
SET @washer  := (SELECT machine_type_id FROM machine_types WHERE name = 'Flat Washer Line');

SELECT @forming AS forming_type_id, @washer AS washer_type_id;

START TRANSACTION;

INSERT INTO machines
  (machine_name, machine_code, machine_type_id, is_active, is_running, created_at)
VALUES
  ('Forming Machine 1',  'FRM-01', @forming, 1, 0, '2026-09-15 00:00:00.000000'),
  ('Forming Machine 2',  'FRM-02', @forming, 1, 0, '2026-09-15 00:00:00.000000'),
  ('Forming Machine 3',  'FRM-03', @forming, 1, 0, '2026-09-15 00:00:00.000000'),
  ('Flat Washer Line 1', 'FWL-01', @washer,  1, 0, '2026-09-15 00:00:00.000000'),
  ('Flat Washer Line 2', 'FWL-02', @washer,  1, 0, '2026-09-15 00:00:00.000000')
ON DUPLICATE KEY UPDATE
  machine_type_id = VALUES(machine_type_id),
  is_active       = VALUES(is_active);

COMMIT;

-- -----------------------------------------------------------------------------
-- NOTE 1 - `machines` has TWO unique keys: machine_name and machine_code.
--   ON DUPLICATE KEY UPDATE reacts to whichever one collides. If a target
--   database already holds a machine with one of these names but a different
--   code (or the reverse), the existing row is updated instead of a new one
--   being inserted, and the names/codes above will not both end up as written.
--   Check first on a database that already has machines:
--     SELECT machine_id, machine_name, machine_code FROM machines
--     WHERE machine_name IN ('Forming Machine 1','Forming Machine 2',
--                            'Forming Machine 3','Flat Washer Line 1',
--                            'Flat Washer Line 2')
--        OR machine_code IN ('FRM-01','FRM-02','FRM-03','FWL-01','FWL-02');
--
-- NOTE 2 - machine_id is assigned by AUTO_INCREMENT, and it is the foreign key
--   that the IoT-written `oee_data` and `power_data` tables point at. Those
--   tables are compatibility-locked and written by the IoT team, not by this
--   application. If the IoT side is already configured to publish telemetry for
--   specific machine_ids, auto-assigned ids will NOT line up and the telemetry
--   will land against the wrong machine or be rejected by the foreign key.
--   In that case agree the ids first and pin them explicitly instead, e.g.
--     INSERT INTO machines
--       (machine_id, machine_name, machine_code, machine_type_id,
--        is_active, is_running, created_at)
--     VALUES (101, 'Forming Machine 1', 'FRM-01', @forming, 1, 0, '...'), ...
--
-- NOTE 3 - on the current local database machine_id AUTO_INCREMENT stands at 8
--   (ids 5, 6, 7 are the existing 'Test Machine' rows), so these five land on
--   ids 8-12 there. A freshly migrated staging database starts at 1.
-- -----------------------------------------------------------------------------

-- Verify
-- SELECT m.machine_id, m.machine_name, m.machine_code, t.name AS type,
--        t.production_line, m.is_active, m.is_running
-- FROM machines m
-- JOIN machine_types t ON t.machine_type_id = m.machine_type_id
-- ORDER BY m.machine_id;
