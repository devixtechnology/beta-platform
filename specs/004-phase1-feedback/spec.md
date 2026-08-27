# Feature Specification: Phase 1 Client Feedback

**Feature Branch**: `004-phase1-feedback`

**Created**: 2026-08-23

**Status**: Draft

**Input**: User description: "please start handle this comments" — the 8 items in the client document *BETA Platform Comments (Phase 1)*.

## Context

The client reviewed the deployed Phase 1 platform and returned eight written comments covering
account management, status consistency, live data refresh, work-order visibility, a production-floor
display, a metric rename, and a wording fix. This feature turns those comments into a single
deliverable. Comment 3 in the document is a **question** about how the 24-hour window behaves, not a
change request; it is answered here and converted into an explicit, testable requirement so the
behaviour stops being ambiguous.

## Clarifications

### Session 2026-08-23

Planning proceeded without a live clarification round, so the four open decisions were resolved by
taking the recommended option and recording it here. Each is **provisional pending client
confirmation** — see `research.md` for the reasoning and the cost of reversing each one.

- Q: What may client user accounts change, not just view? → A: They run production — create/edit work orders, record input weights, and change work order status. Machines, machine types, products, and user management stay administrator-only.
- Q: Do the uptime/downtime figures cover a rolling 24 hours or reset at midnight? → A: Rolling last 24 hours (current behaviour), with the period stated explicitly on screen.
- Q: Does the production-floor big screen require sign-in? → A: Yes — same authentication as every other screen, signed in once on the display using a dedicated display account; the session renews itself while the screen is open.
- Q: How old may a telemetry reading be before a machine's status is treated as unknown? → A: 5 minutes, adjustable by configuration without a code change.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consistent machine status on every screen (Priority: P1)

An operator looks at the live monitoring screen and sees a machine reported as **Stopped**. They open
the machines list and the same machine is reported as **Running**. Today those two screens read from
different places, so the platform contradicts itself and the operator cannot trust either number.
Every screen that shows a machine's running state must show the same value, derived from the same
source, at the same moment.

**Why this priority**: A monitoring platform that reports two different states for the same machine
loses all credibility. This is a correctness defect, it is visible on the first screen a client opens,
and it undermines trust in every other number the platform shows.

**Independent Test**: With a machine whose latest telemetry reports "stopped", open the live
monitoring screen, the machines list (both list and card layouts), and the machine detail screen —
all three must read *Stopped*. Flip the telemetry to "running" and repeat; all three must read
*Running* within one refresh cycle.

**Acceptance Scenarios**:

1. **Given** a machine whose most recent telemetry reading reports it as stopped, **When** a user opens the machines list, **Then** that machine is shown as *Stopped*, matching the live monitoring screen.
2. **Given** a machine whose most recent telemetry reading reports it as running, **When** a user views the machines list, the card layout, the machine detail screen, and the live monitoring screen, **Then** all four show *Running*.
3. **Given** a machine that has never reported any telemetry, **When** a user views any screen showing its status, **Then** every screen shows the same *Stopped* state rather than reporting it as *Running*.
4. **Given** a machine that has been deactivated, **When** a user views the machines list, **Then** it is clearly marked inactive and its telemetry-derived status is not presented as live.

---

### User Story 2 - Client accounts and access management (Priority: P1)

The platform currently has one shared administrator account with a well-known default password. The
client needs their own named accounts — five to start — and the administrator needs a screen where
they can create, edit, deactivate, and reset passwords for those accounts without asking the vendor.
The administrator also needs to change the default administrator password immediately.

**Why this priority**: The default administrator password is a live security exposure, and shared
credentials make every action untraceable. Named accounts are a precondition for handing the platform
over to the client.

**Independent Test**: Sign in as the administrator, change the administrator password, sign out, sign
back in with the new password, create five client accounts, and confirm each can sign in and sees
only what its role permits.

**Acceptance Scenarios**:

1. **Given** a signed-in administrator, **When** they change their own password to one meeting the password rules, **Then** the old password no longer works and the new one does.
2. **Given** a signed-in administrator, **When** they create a client account with an email and initial password, **Then** that account can sign in and reaches the live monitoring screen.
3. **Given** a signed-in administrator, **When** they view the user management screen, **Then** they see all accounts with their role and active/inactive state.
4. **Given** a client account, **When** it signs in, **Then** the user management screen is unreachable — both from the navigation and by entering its address directly.
5. **Given** an administrator deactivates a client account, **When** that account attempts to sign in, **Then** access is refused with a clear message.
6. **Given** an administrator resets a client account's password, **When** the client signs in with the new password, **Then** access is granted and the previous password is rejected.
7. **Given** an account creation attempt using an email that already exists, **When** it is submitted, **Then** it is rejected with a clear message and no duplicate account is created.

---

### User Story 3 - Machine detail screen updates itself (Priority: P2)

A supervisor opens a machine's detail screen and watches it. Today the numbers freeze at the moment
the page opened — they only change if the supervisor manually refreshes the browser. The detail
screen must keep itself current the same way the live monitoring screen already does.

**Why this priority**: Users treat the detail screen as a live view; stale numbers presented as live
are worse than no numbers. It is the second-most-used screen after live monitoring.

**Independent Test**: Open a machine detail screen, leave it untouched, push new telemetry for that
machine, and confirm the displayed values change on their own within one refresh cycle without any
manual page refresh, and without the page jumping or losing scroll position.

**Acceptance Scenarios**:

1. **Given** an open machine detail screen, **When** new telemetry arrives for that machine, **Then** the status, current values, and 24-hour figures update on their own within one refresh cycle.
2. **Given** an open machine detail screen, **When** it updates, **Then** the page does not scroll, jump, or flicker, and the user's position on the page is preserved.
3. **Given** an open machine detail screen, **When** the connection to the server is temporarily unavailable, **Then** the last known values remain visible and the screen resumes updating once the connection returns.

---

### User Story 4 - Current work order visible on the machine (Priority: P2)

A supervisor standing at a machine wants to know what that machine is making *right now*. Today they
have to leave the machine screen, open the work orders list, and search for an in-progress order
assigned to that machine.

**Why this priority**: It closes the loop between the machine view and the production plan and removes
a daily manual lookup. It depends on no other item and is small.

**Independent Test**: Assign a work order to a machine, move it to in-progress, open that machine's
detail screen, and confirm the order is shown with its key details and a way to open it.

**Acceptance Scenarios**:

1. **Given** a machine with an in-progress work order assigned to it, **When** a user opens that machine's detail screen, **Then** the work order is shown with its number, the product being produced, the planned quantity, and how long it has been running.
2. **Given** a machine with an in-progress work order, **When** a user selects the shown work order, **Then** they are taken to that work order's detail screen.
3. **Given** a machine with no in-progress work order, **When** a user opens its detail screen, **Then** a clear "no work order in progress" message is shown instead of an empty area.
4. **Given** a machine with more than one in-progress work order assigned, **When** a user opens its detail screen, **Then** the most recently started one is shown as current and the user is told others exist.

---

### User Story 5 - Input Weight replaces Good Units (Priority: P2)

On the machine summary card the client does not want *Good Units*. They want **Input Weight** — the
weight of raw material loaded before the work order starts — so the floor can see how much material
went in against what is coming out.

**Why this priority**: It changes what the client sees on their most-watched screen, and the
underlying input-weight data already exists. Bundled with the machine screens work.

**Independent Test**: Record input weight against an in-progress work order, open the live monitoring
screen, and confirm the machine card shows *Input Weight* with that value where *Good Units* used to be.

**Acceptance Scenarios**:

1. **Given** a machine with an in-progress work order that has recorded input weight, **When** a user views the machine card on the live monitoring screen, **Then** the card shows *Input Weight* with the total recorded weight, and *Good Units* is no longer shown.
2. **Given** a machine with an in-progress work order that has no recorded input weight yet, **When** a user views its card, **Then** *Input Weight* is shown as zero rather than blank or missing.
3. **Given** a machine with no in-progress work order, **When** a user views its card, **Then** *Input Weight* is shown as zero with no error.
4. **Given** an administrator records an additional input weight on the in-progress work order, **When** the live monitoring screen next refreshes, **Then** the machine card's *Input Weight* reflects the new total.
5. **Given** any screen displaying this figure, **When** the interface language is Arabic, **Then** the label is shown correctly translated.

---

### User Story 6 - Production-floor big screen (Priority: P3)

The client wants a single screen designed to be shown on a large display mounted in the production
area — readable from a distance, showing every machine at once, running unattended all shift with no
one touching it.

**Why this priority**: High visibility and explicitly requested, but it presents information the
platform already produces rather than fixing anything broken. It also benefits from User Story 1
being done first, so it does not put contradictory statuses on a wall-mounted display.

**Independent Test**: Open the big-screen view on a large display, leave it running for a full shift
untouched, and confirm it stays current, stays readable from across the floor, and never shows a
dialog, scrollbar, or navigation element.

**Acceptance Scenarios**:

1. **Given** the big-screen view is opened on a large display, **When** it loads, **Then** every active machine is visible at once with its status, overall effectiveness, and key production figures, with no scrolling required.
2. **Given** the big-screen view is running, **When** telemetry changes, **Then** the display updates on its own without any user interaction.
3. **Given** the big-screen view is running unattended, **When** several hours pass, **Then** it is still displaying current data and has not shown an error dialog, timed out, or navigated away.
4. **Given** the big-screen view is displayed, **When** viewed from across the production area, **Then** machine names, statuses, and key figures are legible at a distance.
5. **Given** there are more active machines than fit legibly at once, **When** the view is displayed, **Then** it cycles through them automatically rather than shrinking everything past legibility.
6. **Given** the big-screen view, **When** it is displayed, **Then** it shows no navigation bar, no editing controls, and no elements that invite interaction.

---

### User Story 7 - Correct product wording (Priority: P3)

The product name is written as two words, "Edge View Platform", in the page footer. It must read as
one word — "EdgeView" — everywhere the product is named.

**Why this priority**: Cosmetic, but it is the client's own product name appearing wrong on every
page, and it costs almost nothing to fix.

**Independent Test**: Visit every screen in both languages and confirm the product name always
appears as one word.

**Acceptance Scenarios**:

1. **Given** any page in the platform, **When** a user views the footer, **Then** the product name reads as one word, "EdgeView".
2. **Given** any screen in the platform, including the sign-in screen and the browser tab titles, **When** the product is named, **Then** it is spelled as one word.

---

### Edge Cases

- A machine reports telemetry, then stops reporting entirely — how long before its status is treated as stale rather than continuing to show the last known state? (See FR-005.)
- Telemetry arrives with an unrecognised status value that is neither running nor stopped — treated as *Unknown*, never as *Running*.
- The administrator attempts to deactivate or delete the last remaining administrator account — must be refused so the platform cannot be locked out.
- A client account is deactivated while signed in and actively using the platform — access ends at the next request rather than continuing until sign-out.
- A machine's detail screen is open when that machine is deactivated by an administrator in another session.
- A work order is marked finished while its machine's detail screen is open — the current-work-order area must clear itself on the next refresh.
- The big-screen view is opened when no machines are active, or when no machine has ever reported telemetry.
- The network connection drops for an extended period while the big-screen view runs unattended — the display must recover on its own, without anyone visiting the floor to refresh it.
- Two administrators edit the same user account at the same time.
- Input weight is recorded against a work order that is assigned to no machine.

## Requirements *(mandatory)*

### Functional Requirements

#### Machine status consistency

- **FR-001**: The platform MUST derive a machine's running state from one single rule on every screen that displays it: an in-progress work order means *Running*; otherwise the machine's most recent telemetry reading decides. *(Amended 2026-08-27: the work-order override was added; telemetry alone was the original source.)*
- **FR-002**: The machines list, the machines card layout, the machine detail screen, the live monitoring screen, and the production-floor big screen MUST all show the same state for the same machine at the same moment.
- **FR-003**: A machine with no telemetry reading MUST be shown as *Stopped* on every screen, never as *Running*. *(Amended 2026-08-27: originally *Unknown*. Silence is treated as not producing.)*
- **FR-004**: A machine's administrator-set active/inactive flag MUST NOT be presented as, or confused with, its live running state; the two MUST remain visually distinct.
- **FR-005**: Where a machine's most recent telemetry reading is older than 5 minutes, the platform MUST show it as *Stopped* rather than continuing to report a state that is no longer being confirmed. *(Amended 2026-08-27: originally *Unknown*.)* The threshold MUST be consistent across every screen and MUST be adjustable by configuration without a code change.

#### Accounts and access management

- **FR-006**: The default administrator password MUST be changed as part of this feature, and the platform MUST NOT ship with a publicly known default credential.
- **FR-007**: A signed-in user MUST be able to change their own password after confirming their current password.
- **FR-008**: An administrator MUST be able to create, edit, deactivate, and reactivate user accounts, and reset any account's password.
- **FR-009**: The platform MUST support at least two roles: administrator (full access) and client user.
- **FR-010**: Client users MUST be able to view the live monitoring screen, the machines screens, the products screens, and the work orders screens.
- **FR-010a**: Client users MUST be able to create and edit work orders, record and remove input weights, and change work order status.
- **FR-010b**: Creating, editing, and deactivating machines, machine types, and products MUST be restricted to administrators; client users see those screens read-only, with the editing controls absent rather than present-and-failing.
- **FR-011**: User management MUST be reachable only by administrators, both through navigation and by direct address entry.
- **FR-012**: The platform MUST refuse to create two accounts with the same email address.
- **FR-013**: The platform MUST prevent the removal or deactivation of the last remaining administrator account.
- **FR-014**: A deactivated account MUST be refused sign-in, and an already-signed-in deactivated account MUST lose access on its next request.
- **FR-015**: Passwords MUST meet a stated minimum strength rule, and the rule MUST be shown to the user when they choose a password.
- **FR-016**: The platform MUST be delivered with five client accounts created for the client.
- **FR-017**: All user management screens MUST be available in both Arabic and English, consistent with the rest of the platform.

#### Machine detail screen

- **FR-018**: The machine detail screen MUST refresh its displayed values on its own, without the user refreshing the page.
- **FR-019**: Refreshes MUST NOT cause visible flicker, layout shift, or loss of the user's scroll position.
- **FR-020**: When a refresh fails, the last known values MUST remain visible and refreshing MUST resume automatically once the platform is reachable again.
- **FR-021**: The machine detail screen MUST show the work order currently in progress on that machine, including its number, the product being produced, the planned quantity, and elapsed running time.
- **FR-022**: The shown work order MUST link to that work order's own screen.
- **FR-023**: When no work order is in progress on the machine, the screen MUST show a clear empty-state message.
- **FR-024**: When more than one work order is in progress on the same machine, the most recently started MUST be shown as current, and the user MUST be told that others exist.

#### 24-hour uptime and downtime

- **FR-025**: The platform MUST state, on the machine detail screen itself, what period the uptime and downtime figures cover, so the figure is never ambiguous to the reader.
- **FR-026**: The uptime and downtime figures MUST cover a rolling window of the last 24 hours ending at the present moment, and the screen MUST say so in words rather than leaving "24h" open to interpretation.
- **FR-027**: Uptime and downtime for a machine MUST together account for the whole stated period, and periods with no telemetry at all MUST NOT be silently counted as uptime.

#### Input Weight

- **FR-028**: The machine summary card MUST show *Input Weight* — the total weight of raw material recorded against the machine's in-progress work order — in place of *Good Units*.
- **FR-029**: Where a machine has no in-progress work order, or that order has no recorded input weight, *Input Weight* MUST be shown as zero.
- **FR-030**: The *Input Weight* figure MUST update on the same refresh cycle as the rest of the machine card.
- **FR-031**: The *Input Weight* label MUST be presented correctly in both Arabic and English.

#### Production-floor big screen

- **FR-032**: The platform MUST provide a display view designed for a large screen mounted in the production area.
- **FR-033**: The big screen MUST show all active machines at once with status, overall effectiveness, and key production figures, without requiring scrolling.
- **FR-034**: Where more machines are active than can be shown legibly at once, the view MUST cycle through them automatically.
- **FR-035**: The big screen MUST refresh on its own and MUST run unattended for an entire shift without any user interaction.
- **FR-036**: The big screen MUST present no navigation, no editing controls, and no dialogs.
- **FR-037**: Text and figures on the big screen MUST be sized for reading at a distance across a production area.
- **FR-038**: Access to the big screen MUST require a signed-in session, like every other screen. Someone signs in once on the display using a dedicated display account, and that session MUST stay valid for as long as the screen is open and refreshing, so nobody has to attend the display to sign in again.
- **FR-039**: The big screen MUST recover on its own from a temporary loss of connection, without anyone attending the display.

#### Product naming

- **FR-040**: The product name MUST be written as one word — *EdgeView* — everywhere it appears, including the footer, the sign-in screen, page titles, and browser tab titles, in both languages.

### Key Entities

- **User Account**: A named person who can sign in. Holds an email, a password, a role, an active/inactive state, and a display name. Replaces the single shared administrator login.
- **Role**: Determines what a signed-in user may see and do. At minimum: administrator and client user.
- **Machine Running State**: The live running/stopped/unknown state of a machine, derived from its most recent telemetry reading rather than stored and edited by hand.
- **Current Work Order**: The work order in progress on a given machine, relating a machine to what it is producing right now.
- **Input Weight**: The total weight of raw material recorded against a work order before and during production, already captured against work orders today and now surfaced per machine.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any machine, the running state shown on every screen is identical at any given moment — zero contradictions across the live monitoring screen, machines list, card layout, machine detail screen, and big screen, verified across all machines in both languages.
- **SC-002**: A user watching a machine detail screen sees new telemetry reflected within one refresh cycle without touching the page — 100% of the time, with no manual refresh needed.
- **SC-003**: An administrator can create a new client account and have that person signed in and viewing live data in under 2 minutes, without vendor assistance.
- **SC-004**: No account in the delivered platform uses a default or shared password, and the client has five working named accounts.
- **SC-005**: A client user attempting to reach any administrator-only screen is refused in 100% of attempts, including by entering the address directly.
- **SC-006**: A supervisor can identify what a machine is currently producing from the machine's own screen, with no navigation to any other screen.
- **SC-007**: The big screen runs unattended for a full 8-hour shift while continuing to show current data, with no user interaction and no manual refresh.
- **SC-008**: Machine names, statuses, and key figures on the big screen are legible from the far side of the production area.
- **SC-009**: The product name appears as one word on 100% of screens in both languages; zero occurrences of the two-word form remain.
- **SC-010**: The meaning of the uptime and downtime figures is stated on screen, and the client confirms it matches how they intend to read it.
- **SC-011**: All new screens meet the same accessibility standard as the rest of the platform (WCAG 2.1 AA) in both light and dark themes and in both languages.
- **SC-012**: The live monitoring screen's refresh behaviour is unchanged — no added flicker, no layout shift, and no slower refresh than before this feature.

## Assumptions

- The eight client comments in *BETA Platform Comments (Phase 1)* are the complete scope of this feature; no further items are inferred beyond them.
- Comment 3 in the client document is a question, not a change request. It is answered by making the period explicit on screen (FR-025) and by confirming the intended window (FR-026); the current behaviour is a rolling 24-hour window ending now.
- "Input Weight" refers to the raw-material weight already recorded against work orders, aggregated for the machine's in-progress work order. No new kind of measurement is introduced.
- Telemetry remains read-only and continues to be written by the IoT team. Nothing in this feature writes telemetry, and no ingestion interface is added.
- The five client accounts are created by an administrator through the new user management screens rather than being fixed into the platform, so the client can add or remove accounts later without a release.
- Client users receive their initial password out of band from the administrator; email delivery, self-service password reset, and multi-factor authentication are out of scope.
- Audit logging of user management actions is out of scope for this feature.
- The big screen is shown on a landscape display at approximately 1920×1080 or larger, in one fixed language, and shows machines only — it is not a place to act on the platform.
- The big screen presents information the platform already produces; no new measurement or calculation is introduced for it.
- The live monitoring screen's existing refresh interval and its data contract are unchanged; the machine detail screen and big screen follow the same refresh approach rather than introducing a different one.
- Bilingual Arabic/English support, right-to-left layout, the light/dark theme, and the existing visual design system apply to every screen added or changed here.
- Renaming the product in the interface does not require renaming anything users cannot see.
