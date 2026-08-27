# Contract: OEE & Power Telemetry Database Tables (IoT writer contract)

**Feature**: `001-phase-1-core` | **Direction**: IoT team **writes**, Beta app **reads only**

This is the hard data contract behind FR-050 / FR-051 / FR-042. The IoT team inserts rows directly
into these two MySQL tables using their existing SPackEdgeView writers. Beta Platform MUST create
these tables (via EF Core migration) with **identical** column names, types, precision/scale,
nullability, and the `machine_id` foreign key, and MUST NOT expose any ingestion API or manual-entry
UI for them. The app performs `SELECT` only.

Source of truth: reference project `E:\Projects\Work\S\web` — `Data/Entities/OeeData.cs`,
`Data/Entities/PowerData.cs`, `Data/ApplicationDbContext.cs`, migration `20260412213438_InitialCreate.cs`.

## `oee_data`

```
id            int              NOT NULL  AUTO_INCREMENT  PRIMARY KEY
machine_id    int              NOT NULL  FK -> machines(machine_id) ON DELETE CASCADE
timestamp     datetime(6)      NOT NULL
availability  decimal(5,2)     NOT NULL  DEFAULT 0        -- percentage 0..100
quality       decimal(5,2)     NOT NULL  DEFAULT 0        -- percentage 0..100
performance   decimal(5,2)     NOT NULL  DEFAULT 0        -- percentage 0..100
total_count   decimal(65,30)   NOT NULL  DEFAULT 0        -- MySQL default precision (leave unset)
total_goods   decimal(65,30)   NOT NULL  DEFAULT 0
status        tinyint unsigned NOT NULL  DEFAULT 0        -- 0=Stopped, 1=Running
created_at    datetime(6)      NOT NULL
order_id      int              NULL      FK -> work_orders(work_order_id)  -- set by IoT
```

Indexes: `(machine_id, timestamp)`, `(timestamp)`, `(order_id)`.

**App read**: OEE % = `(availability × performance × quality) / 10000`. Dashboard uses the latest
row per `machine_id` (`ORDER BY timestamp DESC LIMIT 1`).

## `power_data`

```
id          int           NOT NULL  AUTO_INCREMENT  PRIMARY KEY
machine_id  int           NOT NULL  FK -> machines(machine_id) ON DELETE CASCADE
timestamp   datetime(3)   NOT NULL                   -- millisecond precision (NOT datetime(6))
kw_hr       decimal(10,2) NULL                       -- consumed energy (kWh)
v1          decimal(8,2)  NULL
v2          decimal(8,2)  NULL
v3          decimal(8,2)  NULL
v12         decimal(8,2)  NULL
v23         decimal(8,2)  NULL
v13         decimal(8,2)  NULL
a1          decimal(8,2)  NULL
a2          decimal(8,2)  NULL
a3          decimal(8,2)  NULL
a_avg       decimal(8,2)  NULL
frequency   decimal(5,2)  NULL
created_at  datetime(6)   NOT NULL
```

Indexes: `(machine_id, timestamp)`, `(timestamp)`.

**App read**: Dashboard shows live power (kW) from the latest row per `machine_id`. Only `kw_hr`
(and voltages/currents if later needed) are surfaced in Phase 1; the rest are stored for
compatibility.

## Compatibility rules (MUST)

1. Column names, types, precision/scale, and nullability **exactly** as above — no additions,
   renames, or precision changes to these two tables in Phase 1.
2. `machine_id` references `machines(machine_id)` (the integer PK), **not** `machine_code`. IoT
   writers must insert a `machine_id` that exists in `machines`.
3. Note the timestamp-precision difference: `oee_data.timestamp` = `datetime(6)`,
   `power_data.timestamp` = `datetime(3)`; `created_at` = `datetime(6)` in both.
4. `total_count` / `total_goods` must remain at MySQL's default `decimal(65,30)` (do not configure
   precision in fluent mapping) to match the reference writer's expectations.
5. No application code writes to these tables. Reads must degrade gracefully when a machine has no
   rows or stale rows (FR-044).
