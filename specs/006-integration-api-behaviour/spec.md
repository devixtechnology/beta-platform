# Feature Specification: Integration API Behaviour

**Feature Branch**: `006-integration-api-behaviour`

**Created**: 2026-09-11

**Status**: Draft

**Input**: User description: "Make the five business operations of the 005 token-authenticated Integration API real, replacing the contract-first representative responses with platform data, without changing any route, request shape, response shape, or status code. Add the `work_order_input_products` join table anticipated by 005 research R13. Update the Postman collection to carry an explicit bearer header and to drop the slice-boundary warnings."

## Why this feature exists

Feature 005 shipped the Integration API **contract-first**. Authentication, renewal, permissions,
request validation and the error shape were built for real; the five business operations were not.
They answer with a small set of representative, invented records and persist nothing. An integrator
can build a complete client against final shapes today, but every product it reads is made up and
every order it raises disappears.

005 fixed the contract precisely so that finishing the job would be an **internal** change. This
feature is that change: the same six URLs, the same bodies, the same status codes, now answered from
the platform's own data.

### What 005 left deliberately unfinished

| Behaviour | 005 | This feature |
|---|---|---|
| Sign-in, token issuing, renewal, rotation, revocation | **Real** | Untouched |
| Permission enforcement per endpoint | **Real** | Untouched |
| Request-shape validation and the one error shape | **Real** | Untouched |
| Reading the actual product catalogue | Representative | **Real** |
| Persisting a created product | Representative | **Real** |
| Persisting a work order | Representative | **Real** |
| Resolving a product code against the real catalogue | Representative | **Real** |
| Refusing a duplicate product code or work-order number | Documented, unreachable | **Real** |

The lower half was specified in full by 005 — response codes, shapes and messages are already fixed.
Nothing in this feature may change them. If a contract file in
`specs/005-jwt-integration-api/contracts/` has to be edited to describe what was built, the build is
wrong, not the contract. The one permitted exception is the Postman collection, which carries
slice-boundary warnings that stop being true the moment this feature lands (User Story 5).

## Clarifications

### Session 2026-09-11

- Q: How does the existing single input-product reference on a work order relate to the new
  multi-input record? → A: **Primary plus full list.** The existing single reference keeps holding
  the **first** submitted input and stays required, so every existing screen and reporting view is
  untouched. The new record holds the **complete** list, first entry included, in the order
  submitted. Every work order already stored is backfilled with one entry, so "all the inputs of this
  order" is one question with one answer for every order, whenever and however it was created.
- Q: May a deactivated product be named as an input or output of a new work order? → A: **No.** A
  deactivated code is refused exactly as an unknown code is, naming the same field. This matches the
  Work Orders screen, which offers only active products. Reading a deactivated product by code still
  returns it — reading history and raising new work are different acts.
- Q: Do the existing Work Orders screens gain multi-input editing in this feature? → A: **Viewing
  only.** The order-details screen shows every input product; the create and edit forms keep their
  single input selection unchanged.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read the real product catalogue (Priority: P1)

An integration caller lists products and fetches one by code, and gets the plant's actual catalogue
rather than four invented rows. The active-only filter filters real products. A code that exists is
returned; a code that does not exist is refused as missing.

**Why this priority**: It is the foundation everything else stands on. Product creation is only
meaningful once a catalogue exists to conflict with, and a work order can only resolve codes against
a catalogue that is real. It is also the smallest slice that delivers value alone: a caller that only
reads the catalogue already has a working integration on day one.

**Independent Test**: Add three products through the existing Products screen, one of them
deactivated. Call the list endpoint and confirm those three appear with their real names, codes,
categories and units; call it with the active-only filter and confirm the deactivated one drops out;
fetch each by its code, with odd casing and surrounding spaces, and confirm the same product comes
back each time; fetch a code that was never created and confirm it is refused as missing.

**Acceptance Scenarios**:

1. **Given** a catalogue holding products created through the platform's own screens, **When** an
   authenticated caller lists products, **Then** every stored product is returned with its stored
   name, optional English name, optional category, unit and active state — and no internal record
   number in any form.
2. **Given** a catalogue containing both active and deactivated products, **When** the caller lists
   with the active-only filter set, **Then** only active products are returned; **When** the filter
   is absent or unset, **Then** deactivated products are returned too.
3. **Given** a stored product whose code is `RM-STEEL-01`, **When** the caller fetches
   `rm-steel-01`, the same code padded with spaces, or the code exactly, **Then** the same product is
   returned each time.
4. **Given** a stored product that has been deactivated, **When** the caller fetches it by code,
   **Then** it is returned, marked inactive — not hidden. "Never existed" and "no longer used" are
   different answers.
5. **Given** an empty catalogue, **When** the caller lists products, **Then** an empty list is
   returned, not a "nothing found" refusal.
6. **Given** no product carries the code `NOPE-00`, **When** the caller fetches it, **Then** the
   request is refused as addressing something that does not exist.

---

### User Story 2 - Add a product that is really stored (Priority: P2)

An administrative caller creates a product through the API and it appears in the platform — visible
on the Products screen, selectable on the Work Orders screen, and returned by the list operation of
User Story 1. Creating the same code twice is refused the second time.

**Why this priority**: It is the first operation that changes platform state, and it is what turns
the catalogue from something the API reads into something the API participates in. It depends on
User Story 1 only in the sense that a created product must then be readable.

**Independent Test**: Create a product through the API; open the Products screen in a browser and
confirm it is listed with the same code, names, category and unit; fetch it back by code through the
API; submit the same code again — differing only in casing and padding — and confirm the second
attempt is refused as a conflict, not accepted and not reported as a malformed request.

**Acceptance Scenarios**:

1. **Given** an administrative caller and a code no product carries, **When** they create a product,
   **Then** it is stored, reported as created, addressed in the response by its **code**, and
   immediately visible to the platform's own screens.
2. **Given** a product was just created through the API, **When** it is stored, **Then** it is
   **active**, whatever the request contains — the request has no way to say otherwise.
3. **Given** a product already carries the code `RM-STEEL-01`, **When** a caller creates the same
   code in different casing and padding, **Then** the request is refused as a conflict with existing
   data, naming the code.
4. **Given** a caller who is authenticated but not an administrator, **When** they attempt to create
   a product, **Then** they are refused on permission grounds **before** any storage is attempted —
   the refusal 005 already enforces, unchanged.
5. **Given** a product created through the API, **When** an administrator later edits or deactivates
   it on the Products screen, **Then** it behaves exactly like a product created on that screen.
   There are no two kinds of product.

---

### User Story 3 - Raise a work order that is really stored (Priority: P3)

An administrative or client caller raises a work order naming several input products and one output
product, all by code. Every code is resolved against the real catalogue; the order is stored and
appears on the Work Orders screen. A code that resolves to nothing is refused, naming the entry at
fault by its position. An order number already in use is refused as a conflict.

**Why this priority**: It is the feature's end goal and the reason the API exists, but it is last
because it depends on both prior stories: codes can only be resolved once the catalogue is real, and
the conflict behaviour only makes sense once orders are genuinely stored.

**Independent Test**: Raise an order naming two stored input codes and one stored output code;
confirm it appears on the Work Orders screen in the Ready state, with the right products and machine;
raise another naming one input code that no product carries and confirm the refusal names the input
list at the offending position; raise another reusing the first order's number and confirm it is
refused as a conflict.

**Acceptance Scenarios**:

1. **Given** stored products for every submitted code, **When** a permitted caller raises an order,
   **Then** it is stored in the **Ready** state and appears on the Work Orders screen.
2. **Given** an order was stored, **When** the response is read, **Then** the input codes are echoed
   **in the order submitted** and the output code is echoed as submitted, trimmed of accidental
   padding but not otherwise rewritten — and the state is the **name** `Ready`, never a number.
3. **Given** the second of three submitted input codes resolves to no product, **When** the order is
   raised, **Then** it is refused as a malformed request naming `inputProductCodes[1]` — the
   position submitted — and **nothing is stored**.
4. **Given** the output code resolves to no product, **When** the order is raised, **Then** it is
   refused naming `outputProductCode`, and nothing is stored.
5. **Given** several submitted codes resolve to nothing, **When** the order is raised, **Then** the
   refusal names them all in one answer rather than making the caller fix them one round trip at a
   time.
6. **Given** an order already carries the number `WO-1001`, **When** a caller raises another with
   that number, **Then** it is refused as a conflict with existing data, and nothing is stored.
7. **Given** a rework order that consumes and produces the same product, **When** the same code is
   submitted as both an input and the output, **Then** it is **accepted** — this is a legitimate
   order, not a mistake.
8. **Given** an order raised with three input codes, **When** it is stored, **Then** all three input
   products are recorded against it, not just the first.
9. **Given** a stored but **deactivated** product, **When** its code is submitted as an input or as
   the output of a new order, **Then** the order is refused in exactly the way an unknown code is
   refused, naming exactly the same field, and nothing is stored — while fetching that same code
   through the product operation still returns it, marked inactive.
10. **Given** an order that already names a product which is deactivated afterwards, **When** that
    order is read or run, **Then** it keeps working. Deactivation blocks new work, not existing work.

---

### User Story 4 - Several inputs survive storage (Priority: P3)

A work order raised with several input products keeps all of them. The platform's existing storage
records one input product per order; this feature extends it so an order can carry several, without
disturbing the orders already stored or the screens that read them.

**Why this priority**: It is inseparable from User Story 3 — an order that silently kept only its
first input would satisfy every response assertion and still be wrong. It is stated separately
because it is the only part of this feature that changes stored structure, and therefore the only
part that can damage existing data.

**Independent Test**: Raise an order with three input codes through the API; read it back through the
platform's own screens and confirm all three inputs are recorded. Then open a work order that existed
before this feature and confirm it still displays, still edits, and still reports its input product.

**Acceptance Scenarios**:

1. **Given** an order raised with three input codes, **When** its stored inputs are examined,
   **Then** three input products are recorded against it, in the order submitted.
2. **Given** work orders that existed before this feature, **When** the change is applied, **Then**
   every one of them still displays, edits, starts, holds, finishes and reports exactly as before.
3. **Given** the existing Work Orders create and edit forms, **When** a user creates or edits an
   order there, **Then** the forms look and behave exactly as they did, and the resulting order is
   indistinguishable in kind from one raised through the API — it too carries a multi-input record,
   holding its single selection.
4. **Given** the existing reporting views that read work orders, **When** the change is applied,
   **Then** they continue to return the same figures for orders that have a single input.
5. **Given** an order raised through the API with three input products, **When** a user opens it on
   the order-details screen, **Then** all three input products are shown, in the order the caller
   listed them — not just the first.
6. **Given** an order with a single input product, **When** it is opened on the order-details screen,
   **Then** its one input is shown, reading no differently than it did before this feature.
7. **Given** the order's first input product, **When** any order is examined, **Then** it is the same
   product as the order's single input-product reference — the two never disagree.

---

### User Story 5 - The published contract stops lying (Priority: P4)

An integrator opening the Postman collection or the published document sees the behaviour that
actually exists. The warnings saying the data is representative, that nothing is persisted, and that
creating the same product code twice succeeds twice are removed, because none of them is true any
more. Each authenticated request shows its bearer credential explicitly rather than silently
inheriting it, so a reader can see at a glance that the API is token-protected.

**Why this priority**: It changes no behaviour, so it cannot block the others — but shipping the
behaviour while the collection still tells integrators their orders vanish would waste the feature.

**Independent Test**: Open the collection fresh. Confirm each authenticated request displays an
authorization header carrying the stored token. Sign in, then run the collection end to end and
confirm every request passes its own assertions against a live database. Read the collection
description and the published document and confirm neither claims the data is representative.

**Acceptance Scenarios**:

1. **Given** the Postman collection, **When** an integrator inspects any authenticated request,
   **Then** its bearer credential is visible **on the request itself**, not only on the collection.
2. **Given** the collection, **When** it is read end to end, **Then** no text claims the responses
   are representative, that nothing is persisted, or that a duplicate code is accepted twice.
3. **Given** the collection, **When** it is run against a live instance, **Then** it also exercises
   the outcomes that only became reachable in this feature: an unknown code refused as missing, a
   duplicate code refused as a conflict, and an unresolvable input code refused by position.
4. **Given** the published machine-readable document, **When** it is fetched, **Then** its
   description no longer carries the slice note, while every path, shape, status code and security
   scheme in it is **unchanged**.

---

### Edge Cases

- **A code that resolves to a deactivated product, used in a new order.** Refused (FR-028) — the
  answer must be the same for inputs and the output, and must agree with what the Work Orders screen
  offers.
- **The same code submitted twice in one input list.** Already refused by 005's request validation
  before any catalogue is consulted; this feature does not revisit it.
- **A work-order number colliding with one raised a moment earlier by another caller.** Two callers
  can pass the duplicate check simultaneously. The second store must still be refused as a conflict
  rather than stored or surfaced as an unhandled fault.
- **A product code created through the API colliding with one created on the Products screen at the
  same moment.** Same shape of problem, same required answer.
- **A catalogue large enough that returning it whole is slow.** The contract has no pagination and
  cannot gain any without breaking it. See Assumptions.
- **An order that fails on its third input code after two resolved.** Nothing at all may be stored —
  no partial order, no orphaned input records.
- **A product whose optional English name or category is absent.** Must be returned as absent, not as
  an empty value of convenience — 005's representative data deliberately included such a product so
  clients were built for it.
- **An order raised against a machine that does not exist.** The machine is the one identifier this
  API still takes as an internal number; a number matching no machine must be refused, not stored.

## Requirements *(mandatory)*

### Functional Requirements

#### Reading the catalogue

- **FR-001**: The list-products operation MUST return every product stored by the platform, and MUST
  return an empty list — never a "not found" refusal — when the catalogue holds nothing.
- **FR-002**: The active-only filter MUST restrict the result to products that are currently active;
  absent or unset, it MUST include deactivated products.
- **FR-003**: The fetch-by-code operation MUST resolve a code trimmed of surrounding whitespace and
  matched without regard to case, through the single comparison rule the platform already uses
  everywhere else, so the API and the screens never disagree about what "the same code" means.
- **FR-004**: The fetch-by-code operation MUST return a deactivated product, marked inactive, rather
  than treating it as missing.
- **FR-005**: A code carried by no product MUST be refused as addressing something that does not
  exist, in the shape 005 already specified.
- **FR-006**: No internal product record number may appear in any response, in any field, in any
  casing — the guarantee 005 made, which this feature must not quietly break now that responses are
  built from records that have one.

#### Adding a product

- **FR-007**: Creating a product MUST store it so that it is immediately visible to the list and
  fetch operations and to the platform's own screens.
- **FR-008**: A product created through the API MUST be active on creation, whatever the request
  contains.
- **FR-009**: Creating a product whose code is already carried by another product MUST be refused as
  a conflict with existing data, compared by the same trimmed, case-insensitive rule as FR-003.
- **FR-010**: The created-resource address returned to the caller MUST identify the product by its
  **code**.
- **FR-011**: Products created through the API and products created on the Products screen MUST be
  indistinguishable thereafter — same storage, same rules, same screens.

#### Raising a work order

- **FR-012**: Every submitted input code and the submitted output code MUST be resolved against the
  stored catalogue before anything is stored.
- **FR-013**: An input code resolving to no product MUST be refused as a malformed request naming
  `inputProductCodes[i]` at the **position submitted**, counting from zero.
- **FR-014**: An output code resolving to no product MUST be refused as a malformed request naming
  `outputProductCode`.
- **FR-015**: When more than one submitted code fails to resolve, the refusal MUST name every one of
  them in a single answer.
- **FR-016**: A work-order number already in use MUST be refused as a conflict with existing data.
- **FR-017**: A stored order MUST begin in the **Ready** state, and its state MUST be reported by
  name, never by an internal number.
- **FR-018**: The response MUST echo the input codes in the order submitted and the output code as
  submitted, trimmed only of accidental padding.
- **FR-019**: The same code MUST be accepted as both an input and the output — a rework order is
  legitimate.
- **FR-020**: A refused order MUST leave **nothing** stored: no order, no input records, no partial
  state.
- **FR-021**: A machine identifier naming no machine MUST be refused as a malformed request naming
  the machine field, not stored and not surfaced as an unhandled fault.

#### Several inputs per order

- **FR-022**: The platform MUST be able to record more than one input product against a work order,
  in the order the caller listed them.
- **FR-023**: Every work order stored before this feature MUST keep working unchanged — displayed,
  edited, started, held, finished and reported exactly as before.
- **FR-024**: The existing Work Orders screens and the existing reporting views MUST continue to
  function, returning the same figures for any order that has a single input. The **order-details**
  screen MUST additionally show every input product the order carries, so an order raised through
  the API is fully visible to the people running it; the **create and edit** forms keep their single
  input selection and MUST NOT change.
- **FR-025**: An order's existing single input-product reference MUST hold its **first** input and
  MUST stay required. The new multi-input record MUST hold the **complete** list — first entry
  included — in the order submitted. The two MUST agree for every order: the first entry of the list
  is always the same product as the single reference.
- **FR-026**: Every work order already stored MUST be given its multi-input record as part of this
  change, carrying its one existing input product. After the change, no order anywhere in the
  platform may be without one, so no reader ever has to handle an order with no input record.
- **FR-027**: An order created through the existing Work Orders screen MUST get a multi-input record
  too, holding its single selection. An order is an order; there are not two kinds.
- **FR-028**: A code resolving to a **deactivated** product MUST be refused when naming an input or
  the output of a **new** order, in exactly the way an unknown code is refused and naming exactly the
  same field, so a caller fixes both kinds of mistake the same way. This applies identically to
  inputs and to the output.
- **FR-029**: Refusing a deactivated code for a new order MUST NOT change how a deactivated product
  is **read**: the fetch-by-code operation still returns it, marked inactive (FR-004), and orders
  that already reference a since-deactivated product keep working.

#### Contract and collection

- **FR-030**: No route, request field, response field, header or status code defined by 005 may
  change. Swapping representative behaviour for real behaviour MUST be provable as an internal change
  against the existing contract files.
- **FR-031**: Authentication, token renewal, permission enforcement, request-shape validation and the
  error shape MUST NOT be modified.
- **FR-032**: Every authenticated request in the Postman collection MUST carry its bearer credential
  explicitly, visible on the request itself.
- **FR-033**: The collection MUST no longer state that responses are representative, that nothing is
  persisted, or that a duplicate code is accepted twice.
- **FR-034**: The collection MUST exercise the outcomes that become reachable here — a code that
  matches nothing, a duplicate product code, a duplicate order number, and an unresolvable input code
  named by position — kept separate from the happy-path requests so that running the whole collection
  stays predictable.
- **FR-035**: The published machine-readable document MUST drop its slice note while every path,
  shape, status code and security scheme in it stays identical.

#### Storage discipline

- **FR-036**: Every structural change to stored data MUST ship as a versioned, reviewable schema
  migration in the same change set as the code that depends on it. No hand-written schema statements.
- **FR-037**: Concurrent callers submitting the same product code, or the same work-order number, at
  the same moment MUST NOT both succeed; the loser MUST be refused as a conflict, not surfaced as an
  unhandled fault.

### Key Entities

- **Product**: A material or finished good, identified to integrators by its **code** and never by
  its internal record number. Carries a primary name, an optional English name, an optional category,
  a unit, and an active flag. Already stored by the platform; this feature only starts reading and
  writing it through the API.
- **Work order**: A production task in a known state, naming the products it consumes and the one it
  produces, the machine it runs on, when it is planned to start, and how much to make. Already
  stored; this feature adds the ability to record several consumed products.
- **Work-order input product**: The new record linking a work order to one of the products it
  consumes, keeping the caller's ordering. It carries no quantity and no other attribute — 005
  decided deliberately that an input is a code and nothing more, and a field the platform ignores is
  worse than a field that is absent.
- **Work-order input weight** *(existing, unrelated, easily confused)*: The platform already records
  manually-entered input **weights** against an order, each carrying a weight and no product
  reference. That is a different thing from a work-order input product and must not be merged with
  it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A product added through the API is visible on the platform's own Products screen
  immediately, with no further action, in 100% of cases.
- **SC-002**: A work order raised through the API appears on the Work Orders screen in the Ready
  state, with every input and output product it named, in 100% of cases.
- **SC-003**: Every request in the published integration collection passes its own assertions against
  a live instance — including the refusal cases — with no request edited by hand first.
- **SC-004**: The contract files agreed in 005 require **zero** changes to describe what was built,
  other than removing statements that the data is representative.
- **SC-005**: No response from any operation contains an internal product record number, in any field
  or casing — verifiable by scanning the responses of the whole collection.
- **SC-006**: Every work order that existed before this feature still displays, edits and reports
  identically after it — verified against a copy of real data, not an empty database.
- **SC-007**: A submission naming three unresolvable codes tells the caller about all three in one
  answer, so a client can be corrected in a single round trip rather than three.
- **SC-008**: A refused submission leaves the stored data exactly as it was — no order, no input
  record, and no consumed identifier that a later reader has to explain.
- **SC-009**: Every input product of an order raised through the API is visible to a user on the
  order-details screen without leaving the browser — an order never shows less of itself than it
  carries.
- **SC-010**: After the change, **zero** work orders anywhere in the platform lack a multi-input
  record, and for **every** order its first recorded input is the same product as its single
  input-product reference — checkable as two queries that must both return nothing.

## Assumptions

- **No pagination.** The list-products operation returns the whole catalogue, because 005's contract
  defines no paging parameters and adding them would break the contract this feature exists to
  honour. This is acceptable at the plant's catalogue size. If the catalogue ever grows past what a
  single response should carry, paging is a **new contract version**, not a change to this one.
- **No new endpoints.** Reading a work order back, listing work orders, updating a product and
  deactivating one are all absent from 005 and stay absent. A caller verifies a stored order through
  the platform's screens for now.
- **The existing services are the way in.** The platform already has services that own product and
  work-order rules, including the duplicate-code and duplicate-number checks. This feature routes the
  API through them rather than writing a second set of rules that could drift from the screens'.
- **Client callers may raise orders but not create products.** Unchanged from 005, which already
  enforces it.
- **Timestamps and time zone.** Stored orders and products take their creation time from the
  platform's existing clock convention; this feature introduces no second convention.
- **The five seeded client accounts** from the most recent change remain the integration accounts;
  this feature adds none and changes none.
- **Existing rows can be backfilled unambiguously.** Every work order stored today has exactly one
  input product, and it is required, so giving each one a single-entry multi-input record (FR-026)
  needs no guesswork and no human decision.
- **A deactivated product is a business signal, not a data problem.** Deactivation exists to keep a
  product out of *new* selections while leaving history intact, which is why FR-028 refuses it for a
  new order and FR-004 still returns it when read.

## Out of Scope

- Any change to authentication, renewal, permissions, request validation or the error shape.
- Any new route, request field, response field or status code.
- Per-input quantities on a work order — 005 research R13 declined them deliberately, and nothing
  here changes that reasoning.
- Multi-input **editing** in the browser. The order-details screen learns to show several inputs
  (FR-024); the create and edit forms keep their single selection. Teaching those forms to manage a
  list means new form controls, new validation, new AR/EN strings and new preview-pane behaviour —
  a feature of its own, not a corner of this one.
- Retiring the single input-product reference on a work order. It stays required and keeps holding
  the first input (FR-025), because the existing screens and the running-orders reporting view read
  it directly.
- Pagination, filtering or sorting parameters beyond the existing active-only filter.
- Reading work orders back through the API.
- Changes to the telemetry tables, which remain read-only and compatibility-locked.
