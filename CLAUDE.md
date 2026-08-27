<!-- SPECKIT START -->
Active feature: **004-phase1-feedback** — Phase 1 Client Feedback (the 8 client comments in
*BETA Platform Comments (Phase 1)*).

Read the current plan and its design artifacts for full context:
- Plan: `specs/004-phase1-feedback/plan.md`
- Research & decisions: `specs/004-phase1-feedback/research.md`
- Data model (no schema change): `specs/004-phase1-feedback/data-model.md`
- Contracts: `specs/004-phase1-feedback/contracts/` (machine-status, machine-live-data,
  user-management, production-display)
- Validation guide: `specs/004-phase1-feedback/quickstart.md`
- Constitution (non-negotiable): `.specify/memory/constitution.md`

004 scope: **no EF migration** — every field needed already exists. Deliverables: (a) one source of
truth for machine running state — an in-progress work order means `Running`, else the latest
`oee_data` row, falling back to `Stopped` when absent or older than `Telemetry:StaleAfterMinutes`
(default 5) — consumed by dashboard, machines list/cards, details, and display, replacing
`Machine.IsRunning` in the views; (b) user administration on the existing Identity tables — new
`Client` role, `/Users/*` (admin only), `/Account/ChangePassword`, deactivation via security-stamp
rotation, no default admin password in Production; (c) `GET /Machines/Data/{id}` + `machine-details.js`
so the details page self-refreshes every 5 s in place, including a current-work-order card;
(d) **Input Weight** replaces Good Units on the machine card (sum of `work_order_inputs.weight` for the
in-progress order); (e) `/Dashboard/Display` — chromeless big screen on `_DisplayLayout`, reusing
`/Dashboard/Data`, rotating >6 machines; (f) uptime/downtime rewritten to duration-weighted with a
"no data" bucket, labelled "last 24 h"; (g) rename to one word **EdgeView** in the .resx files.
Dashboard ~5 s polling and its data contract stay unchanged; the per-machine N+1 in `DashboardService`
is replaced by one latest-per-machine query. The Client role may run work orders; machines, products,
and users stay admin-only. Four decisions in research.md (D1–D4) are **provisional pending client
confirmation**.

Prior features (foundation, still current): **001-phase-1-core** (`specs/001-phase-1-core/`),
**002-ui-modernization** (`specs/002-ui-modernization/` — design system, theme toggle, creation
pages). Change request **003** shipped without a spec folder: `oee_data` total_weight/total_count
split, single live work-order output, work-order inputs, Edge View rebrand, work-order hour rate /
setup time / capability fields (migrations `20260709193126`, `20260709195313`).

Stack: ASP.NET Core .NET 9 MVC monolith, EF Core 9 + Pomelo MySQL 8.0, ASP.NET Core Identity,
bilingual AR/EN + RTL via IStringLocalizer/.resx. All schema via EF Core migrations only (no raw SQL
DDL). `oee_data` and `power_data` are compatibility-locked, read-only telemetry tables written by the
IoT team — never add an ingestion API. Dashboard refreshes by ~5 s client polling (no SignalR).
Reference project for schema & front-end reuse: `E:\Projects\Work\S\web`.
<!-- SPECKIT END -->
