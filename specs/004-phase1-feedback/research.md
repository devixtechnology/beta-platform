# Phase 0 Research: Phase 1 Client Feedback

**Feature**: `004-phase1-feedback` | **Date**: 2026-08-23

Two kinds of question are resolved here. **D1–D4** are product decisions the client has not yet
confirmed — each was taken as the recommended option so planning could proceed, and each records what
it costs to reverse. **T1–T4** are implementation questions with no client involvement.

---

## D1 — What may client user accounts change?

**Decision**: Two roles. `Admin` keeps everything. `Client` may view every operational screen and may
**run production**: create and edit work orders, record and remove input weights, and change work
order status. Machines, machine types, products, and user administration are administrator-only.

**Rationale**: The client asked for five named accounts and for user access management to be
"activated" — the point of which is that the plant operates the platform itself. Work orders and
input weights are daily operational data entry; routing them through an administrator would leave the
client dependent on the vendor for ordinary work and would push everyone back onto the shared admin
login the feature exists to retire. Master data is different: a deleted machine or product is a
lasting mistake, and it changes rarely, so administrator-only costs the client almost nothing.

**Alternatives considered**:
- *Strictly read-only clients* — simplest to implement and safest, but leaves the client unable to
  record the very input weights comment 7 asks to display. Rejected as self-defeating.
- *Full access except user management* — one attribute on one controller, but lets any of the five
  accounts delete a machine and take its telemetry history off the screens.
- *Read-only plus status transitions* — a coherent middle ground, but work orders would still have to
  be created by an administrator, which is the bulk of the data entry.

**Cost of reversal**: Low. Permissions are `[Authorize(Roles = "Admin")]` attributes on controller
actions plus the matching `IsInRole` checks that hide the buttons. Tightening to read-only is moving
attributes; loosening is removing them. No data model or screen changes either way.

---

## D2 — Rolling 24 hours, or reset at midnight?

**Decision**: Keep the rolling window — the last 24 hours ending now — and say so on screen. The
labels become "Uptime (last 24 h)" / "Downtime (last 24 h)" rather than the ambiguous "24hrs", in
both languages.

**Rationale**: This is the client's own question (comment 3), asked to confirm what they were
looking at, not to demand a change. A rolling window always describes a full day of operation, which
is what an operations screen wants; a midnight-reset figure reads as near-zero downtime at 00:30 and
tempts a false "good shift" conclusion. Most of the confusion is the missing label, not the window —
so the cheap fix addresses the actual complaint, and the expensive one stays available.

**Alternatives considered**:
- *Reset at midnight* — matches daily reporting and shift handover, and may well be what they want.
  Not chosen unilaterally, because it changes a number the client is already reading.
- *Both, with a toggle* — serves every reading but adds a control, a state, and a second calculation
  path for a question that may have an obvious answer once asked. Rejected under YAGNI.
- *Reset at shift start* — plausible for a plant, but nothing in the platform models shifts today.

**Cost of reversal**: Low. The window start is one expression in `MachineService.GetDetailsAsync`
(`now.AddHours(-24)` becomes `now.Date`), plus the label. If they want both, the toggle is a query
parameter on the same method.

---

## D3 — Does the production-floor display require sign-in?

**Decision**: Yes. The display view sits behind the same authentication as every other screen. An
administrator creates a dedicated display account (`Client` role), signs in on the display once with
"remember me", and the session renews itself on every poll for as long as the screen is open.

**Rationale**: The comment asks for a screen, not for an anonymous endpoint. Making it reachable
without authentication would put production data at a guessable address for anyone on the network,
and it is the kind of exception that is never revisited. The unattended-display objection — that a
session expires and someone must walk to the floor — does not apply here: the cookie already uses
sliding expiration with an 8-hour window, and the screen polls every 5 seconds, so the session is
renewed continuously and cannot idle out while the screen is live. A dedicated account also means the
display can be revoked without touching anyone's personal login, and its role keeps it away from
anything destructive.

**Alternatives considered**:
- *Anonymous access* — zero maintenance, but exposes production data to the whole network and creates
  a permanent hole in an otherwise globally-authorized application.
- *Network/IP restriction instead of login* — moves the control into infrastructure the project does
  not own, and cannot be verified from the application's own tests.

**Cost of reversal**: Low but one-directional. Opening it up later is one `[AllowAnonymous]`; closing
it after the client has grown used to an open screen is a conversation. Starting closed is the
recoverable direction.

---

## D4 — How stale may a telemetry reading be before status is Unknown?

**Decision**: 5 minutes, read from configuration as `Telemetry:StaleAfterMinutes` (default 5) so it
can be tuned per site without a release.

**Rationale**: Without an age limit, "latest reading" silently becomes "last reading ever" — a
machine that stopped reporting three weeks ago would display a confident *Running* badge forever,
which is exactly the class of untrustworthy status this feature exists to eliminate. The IoT team's
write cadence is not documented in this repository, so any fixed number is a guess; 5 minutes is
loose enough to survive a slow writer or a brief network gap and tight enough that a genuinely dead
machine is flagged within one coffee break. Making it configuration means a wrong guess is corrected
in `appsettings.json`, not in a patch.

**Alternatives considered**:
- *No threshold* — the current behaviour; leaves stale data indistinguishable from live data.
- *30 seconds* — sharper, but would flap to *Unknown* on any writer hiccup and train users to ignore
  the badge.
- *Derive the threshold from the observed write interval per machine* — self-tuning and appealing,
  but it is inference machinery serving a number a human can simply set. Rejected under YAGNI.

**Cost of reversal**: None — it is a configuration value.

---

## T1 — Fetching the latest telemetry row per machine in one query

**Decision**: One LINQ join against a grouped max-timestamp subquery, exposed as
`IMachineStatusService.GetLatestAsync(...)` returning a dictionary keyed by machine id:

> group `oee_data` by `machine_id`, select `(machine_id, max(timestamp))`, then join that back to
> `oee_data` on both columns to recover the whole row.

**Rationale**: `DashboardService` currently issues two queries **per machine** inside a `foreach`
(latest OEE, latest power), so a 30-machine plant costs 60 round trips every 5 seconds — and the new
display screen and machines list would each repeat that pattern. The group-then-join shape is plain
LINQ, translates to a single SQL statement on Pomelo/MySQL, and needs no raw SQL, keeping
Principle IV intact. `GroupBy(...).Select(g => g.OrderByDescending(...).First())` was rejected because
its translation is provider-dependent and silently falls back to client evaluation.

**Alternatives considered**: raw SQL with a window function (fastest, but prohibited by Principle IV
without justification, and unnecessary here); caching the latest row in memory (adds invalidation
concerns to fix a problem one query already solves).

---

## T2 — Uptime and downtime accounting with gaps in telemetry

**Decision**: Account by **interval duration**, not sample count. Walk the window's readings in
order; each reading contributes the time until the next reading to *uptime* or *downtime* according to
its status, capped at the staleness threshold from D4. Time beyond that cap, and any stretch with no
readings at all, is a third bucket — "no data" — which is shown only when it is non-zero.

**Rationale**: The current calculation is `runningSamples / totalSamples × 24 h`, which assumes a
perfectly even reporting cadence. If the writer pauses for six hours while a machine is running, those
six hours are shared out across both figures and the platform reports downtime that never happened —
and FR-027 explicitly forbids counting an absence of telemetry as uptime. Duration-weighting with a
capped gap is the smallest change that makes the number mean what its label says, and it needs no new
data. The third bucket is what makes the total honest: uptime + downtime + no-data = the full window.

**Alternatives considered**: keeping the sample-ratio (cheapest, but knowingly wrong when telemetry
gaps exist, and it cannot satisfy FR-027); reconstructing state-change events into runs (more precise
and more code, for a figure displayed to the minute).

---

## T3 — Keeping the unattended display signed in

**Decision**: Sign in on the display with "remember me" for a persistent cookie, and rely on the
existing sliding expiration, which every 5-second poll renews. Set `SlidingExpiration = true`
explicitly in `ConfigureApplicationCookie` so the behaviour is stated rather than inherited from a
framework default.

**Rationale**: The requirement is that nobody has to attend the display. A persistent cookie survives
a browser or machine restart after a power cut; sliding expiration means the session cannot idle out
while the page is actively polling. Together they cover both failure modes with configuration that
already exists — no background keep-alive, no long-lived token, no second authentication path.

**Alternatives considered**: a very long fixed `ExpireTimeSpan` (postpones the problem to a
scheduled outage rather than removing it); an API key for the display (a second authentication
mechanism to build and secure, for one screen).

---

## T4 — Making a deactivated account lose access on its next request

**Decision**: On deactivation, set `IsActive = false` and call `UserManager.UpdateSecurityStampAsync`.
Configure `SecurityStampValidatorOptions.ValidationInterval` to 1 minute so existing cookies are
re-checked against the store and rejected. Sign-in also rejects inactive accounts explicitly, with a
message distinct from "wrong password".

**Rationale**: `ApplicationUser.IsActive` already exists and is already persisted — it is simply not
enforced anywhere today. Rotating the security stamp is Identity's own mechanism for invalidating
issued cookies, so no custom middleware or claims transformation is needed. A 1-minute interval bounds
the exposure without adding a database round trip to every 5-second poll, which
`ValidationInterval = Zero` would.

**Alternatives considered**: Identity lockout with a far-future `LockoutEnd` (works for sign-in, but
overloads a field that means something else, and reads badly on the user administration screen);
checking `IsActive` in a global filter (a database hit per request, re-implementing what the security
stamp validator already does).

---

## Open items to confirm with the client

Before the accounts slice ships, confirm **D1** (what the five accounts may change) and **D2** (rolling
window vs. midnight reset — this was their question). **D3** and **D4** are safe defaults that need no
sign-off, though D4's 5-minute threshold should be sanity-checked against the IoT team's actual write
cadence, which is not recorded in this repository.
