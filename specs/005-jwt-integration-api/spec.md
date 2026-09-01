# Feature Specification: Token-Authenticated Integration API (Products & Work Orders)

**Feature Branch**: `005-jwt-integration-api`

**Created**: 2026-08-28

**Status**: Draft (amended 2026-08-29 — token renewal added, see FR-036 … FR-041; amended
2026-09-01 — a work order names **several** input product codes, see FR-042 … FR-044)

**Input**: User description: "hi i want to make apis for some operations, just apis without logic for now, the first api is for product creation and get one product and get all products and endpint for create work order but note in product for inour and output use the product code and ofcours login and jwtvtoken based endopints"

## Overview

The platform today is reachable only through its browser screens. This feature opens a small,
machine-consumable surface so an external caller — a partner system, a mobile client, or the
plant's own tooling — can sign in, keep its access alive, read and add products, and raise a work
order without a human at a keyboard.

This slice is **contract-first and deliberately behaviour-light**. What ships is the agreement
between the platform and its callers: the endpoint addresses, the request and response shapes, the
authentication, the permission rules, the request-shape validation, and one common error format.
**Authentication is real** — a caller signs in against a genuine platform account, receives a
genuine token, can genuinely renew it, and permission rules are genuinely enforced. The five
business operations are **not yet wired to the platform's data**: they accept and validate a request and answer with a
representative response of the correct shape, persisting nothing and reading nothing. An integrator
can therefore build and test a complete client against a stable contract while the behaviour behind
it is settled in a later slice.

A caller identifies products by their **product code** — the code the plant already prints, speaks,
and files by. Internal record numbers are never required from, nor meaningful to, an external
caller, and this is most visible when raising a work order: its input and output products are named
by code, not by internal identifier. An order names **several** input codes and one output code — a
production task consumes a set of materials and produces one end product.

### Slice boundary: what is real and what is representative

| Behaviour | This slice |
|---|---|
| Sign-in against a real account, real password check, real active/inactive check | **Real** |
| Token issuing, expiry, signature verification, rejection of bad tokens | **Real** |
| Token renewal from a refresh token, with rotation, re-read roles, and revocation | **Real** |
| Permission enforcement per endpoint (administrative vs client vs any authenticated) | **Real** |
| Request-shape validation — required fields, lengths, non-positive quantity, unparsable body | **Real** |
| One shared error shape and correct response code per outcome | **Real** |
| Reading the actual product catalogue from platform data | Representative response |
| Persisting a created product or work order | Representative response |
| Resolving a product code against the real catalogue | Representative response |
| Rejecting a duplicate product code or work-order number | Documented in the contract, not yet produced |

Every row in the lower half is **specified here in full** — response codes, shapes, and messages are
fixed now — so that wiring the behaviour later is an internal change and never a contract change.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Obtain an access token (Priority: P1)

An integration caller presents the credentials of a platform account and receives a time-limited
access token, together with a longer-lived refresh token it can exchange for a new pair when the
first expires. Every subsequent call carries the access token. Without it, nothing else in this
feature is reachable. This story is delivered as fully working behaviour, not as a representative
response.

**Why this priority**: Every other endpoint is gated behind it, and it is the one part of this slice
that is genuinely functional end to end. On its own it already delivers value: it proves an
integration account exists, is active, and can authenticate.

**Independent Test**: Call the sign-in endpoint with valid credentials for a real account and
confirm both tokens and their expiries come back; call it with a wrong password, a deactivated
account, and a missing field, and confirm each is rejected distinctly and without revealing which
part was wrong; exchange the refresh token for a new pair and confirm the old refresh token has been
replaced by a new one in the response.

**Acceptance Scenarios**:

1. **Given** an active account with a correct email and password, **When** the caller signs in,
   **Then** the response carries an access token, the moment it expires, a refresh token, the moment
   *it* expires, and the account's assigned roles.
2. **Given** a correct email with a wrong password, **When** the caller signs in, **Then** the
   request is rejected as unauthorized with a generic message that does not disclose whether the
   email exists.
3. **Given** an account that has been deactivated, **When** the caller signs in with otherwise
   correct credentials, **Then** the request is rejected and no token is issued.
4. **Given** a request with a missing or malformed email or password, **When** it is submitted,
   **Then** it is rejected as invalid input, naming the offending fields.
5. **Given** a valid token, **When** it is presented to any protected endpoint in this feature,
   **Then** the call is accepted and attributed to that account.
6. **Given** an expired, altered, or absent token, **When** it is presented to a protected endpoint,
   **Then** the call is rejected as unauthorized.
7. **Given** a token issued 8 hours ago, **When** it is presented, **Then** it is rejected as
   expired and the caller renews — with its refresh token, or by signing in again — to continue.
8. **Given** a valid refresh token, **When** the caller presents it to the renewal endpoint,
   **Then** it receives a new access token and a **new** refresh token, in the same shape sign-in
   returns, without having sent a password.
9. **Given** a refresh token whose account has since been deactivated, or whose password has since
   been changed, **When** renewal is attempted, **Then** it is refused — the renewal window is not a
   way around withdrawal of access.
10. **Given** a refresh token issued before a role was granted or withdrawn, **When** the caller
    renews, **Then** the new access token carries the account's roles **as they are now**, not as
    they were at sign-in.
11. **Given** an expired, altered, or unknown refresh token, or an access token presented in its
    place, **When** renewal is attempted, **Then** it is refused identically in every case, and the
    caller signs in with credentials again.

---

### User Story 2 - Read the product catalogue (Priority: P2)

An authenticated caller lists the products the plant works with, or fetches a single one by its
product code, so it can build and exercise the client-side handling of the catalogue against a
stable shape.

**Why this priority**: Reading is the safest capability and the one integrators code against first;
it is testable the moment sign-in works. In this slice it answers with a representative catalogue
rather than the plant's real one.

**Independent Test**: With a valid token, fetch the list and confirm every entry carries code,
names, category, unit, and active flag; fetch one by code and confirm the same shape comes back;
fetch without a token and confirm the call is refused.

**Acceptance Scenarios**:

1. **Given** an authenticated caller, **When** it requests all products, **Then** it receives a list
   whose every entry carries product code, primary name, English name, category, unit of measure,
   and whether the product is active.
2. **Given** an authenticated caller, **When** it requests all products restricted to active ones,
   **Then** the request is accepted and answered in the same shape — the filter is part of the
   agreed contract even though it selects nothing yet.
3. **Given** an authenticated caller and any product code, **When** it requests that single product,
   **Then** a single product of the agreed shape is returned.
4. **Given** no token, **When** either read is attempted, **Then** the call is rejected as
   unauthorized.
5. **Given** the contract document, **When** an integrator reads it, **Then** the "not found"
   response for an unknown product code is fully specified, so the client can handle it before the
   platform can produce it.

---

### User Story 3 - Add a product (Priority: P2)

An authorized caller submits a new product — its code, names, category, and unit — and receives back
the created representation. The submission is genuinely validated and genuinely permission-checked;
it is not yet stored.

**Why this priority**: It shares the P2 slot with reading because the catalogue's read and write
shapes are agreed together, but it carries the stricter permission, so it is listed second.

**Independent Test**: With an administrative token, submit a well-formed product and confirm the
response echoes it in the read shape; submit one missing a required field and confirm it is refused
naming that field; submit with a client-role token and confirm it is refused as forbidden.

**Acceptance Scenarios**:

1. **Given** an authorized caller, **When** it submits a well-formed product, **Then** the response
   reports it as created and returns it in the same shape the read endpoints use.
2. **Given** a submission missing a required value, or exceeding an allowed length, **When** it is
   submitted, **Then** it is rejected as invalid input, naming each offending field.
3. **Given** a request body that cannot be parsed, **When** it is submitted, **Then** it is rejected
   as invalid input in the standard error shape, never as an unhandled server error.
4. **Given** an authenticated caller without the permission to manage products, **When** it attempts
   to create one, **Then** the call is refused as forbidden — distinctly from an unauthenticated
   rejection.
5. **Given** the contract document, **When** an integrator reads it, **Then** the conflict response
   for a product code already in use is fully specified, so the client can handle it before the
   platform can produce it.

---

### User Story 4 - Raise a work order by product code (Priority: P3)

An authorized caller submits a work order, naming its input and output products **by product code**
rather than by any internal record number, along with the order number, planned start, quantity, and
the optional machine, rate, setup-time, and capability figures. The inputs are a **list** — an order
consumes several materials — while the output stays a **single** product.

**Why this priority**: It is the feature's end goal and the reason the product endpoints exist, but
it depends on both a token and an agreed product shape, so it lands last.

**Independent Test**: With an authorized token and a few product codes, submit a work order naming
two inputs and one output and confirm the response echoes every code, in the order sent, with an
initial status; submit one omitting the order number, carrying an empty input list, or carrying a
non-positive quantity, and confirm each is refused naming the field.

**Acceptance Scenarios**:

1. **Given** an authorized caller, **When** it submits a well-formed work order naming one or more
   input product codes and an output product code, **Then** the response reports it as created and
   carries the order number, status, planned start, quantity, and **every product code exactly as
   submitted**, the inputs in the order they were sent.
2. **Given** a submission that omits the order number, the input list, the output code, or the
   planned start, **When** it is submitted, **Then** it is rejected as invalid input naming each
   offending field.
3. **Given** a quantity of zero or below, **When** it is submitted, **Then** it is rejected as
   invalid input.
4. **Given** a submission whose output code repeats one of its input codes, **When** it is
   submitted, **Then** it is accepted — a rework order legitimately consumes and produces the same
   product.
5. **Given** a submission carrying an empty input list, or one whose entries include a blank code or
   the same code twice, **When** it is submitted, **Then** it is rejected as invalid input naming
   the input list — an order that consumes nothing, or that names a material twice with no quantity
   to distinguish the entries, is a client-side mistake rather than an instruction.
6. **Given** a created work order, **When** the response is read, **Then** its status is the
   platform's initial "ready" state; this feature exposes no way to start, hold, or finish an order.
7. **Given** an authenticated caller holding neither the administrative nor the client permission,
   **When** it attempts to raise a work order, **Then** the call is refused as forbidden.
8. **Given** the contract document, **When** an integrator reads it, **Then** the responses for an
   unresolvable product code — naming **which entry of the input list** failed, by its position —
   and for a duplicate work-order number are fully specified, so the client can handle both before
   the platform can produce them.

---

### Edge Cases

- **Token presented after expiry** — rejected as unauthorized, distinguishable by the caller from a
  malformed token, so an integration knows to renew rather than to alert an operator.
- **Account deactivated while a token is still valid** — the token MUST stop being honoured, and its
  refresh token MUST stop renewing; withdrawal of access cannot wait out a token's 8-hour life, and
  still less a refresh token's longer one.
- **A refresh token presented as an access token, or an access token presented for renewal** — both
  MUST be refused. The two credentials are not interchangeable, and a caller must not be able to
  turn one into the other.
- **A refresh token used twice** — the contract MUST state plainly what happens, so nobody has to
  discover it: each renewal issues a replacement, and the caller is expected to advance; this slice
  keeps no ledger of issued tokens, so a superseded one is not separately invalidated before its own
  expiry (see Assumptions).
- **Refresh token presented after its own expiry** — refused; the caller signs in with credentials
  again. No credential in this feature lives forever.
- **Sign-in attempted repeatedly with a wrong password** — every attempt is refused identically; the
  contract does not expose whether an account exists, is locked, or is merely mistyped.
- **Product code differing only by letter case or surrounding spaces** — the contract MUST state the
  matching rule now (see Assumptions), because a client built against one rule and a platform later
  wired to another produces failures nobody can reproduce.
- **Malformed or non-parsable request body** — rejected as invalid input with the same error shape
  as any other validation failure, never as an unhandled server error.
- **Request carrying unknown extra fields** — accepted and ignored rather than refused, so a caller
  running slightly ahead of the contract is not broken by it.
- **Empty catalogue** — the list endpoint returns an empty list, not a "not found".
- **Work order carrying an empty input list** — refused, in the same way and for the same reason as
  a quantity of zero: an order that consumes nothing is a mistake worth catching at the edge.
- **Work order naming the same input code twice** — refused. The contract attaches no quantity to an
  input, so a repeat adds nothing a single entry does not say, and is far likelier to be a client
  that built its list wrongly than a deliberate instruction.
- **Work order naming many inputs** — accepted; the list has no fixed ceiling, because a plausible
  order size is a plant's business and inventing a limit would eventually refuse a legitimate
  order.
- **Responses that this slice cannot yet produce** — unknown product code, duplicate product code,
  duplicate work-order number, unresolvable input or output code. Each is fully specified in the
  contract and MUST NOT change shape or response code when the behaviour is wired later.

## Requirements *(mandatory)*

### Functional Requirements

#### Authentication & authorization — delivered as working behaviour

- **FR-001**: The platform MUST expose a sign-in endpoint that accepts an email and password and,
  for a valid and active account, returns a signed bearer token, the moment that token expires, and
  the roles held by the account.
- **FR-002**: An issued token MUST remain valid for **8 hours** from issue — one plant shift — after
  which the caller signs in again to obtain a new one.
- **FR-003**: The platform MUST let a caller renew without re-sending credentials, by exchanging a
  refresh token issued alongside the access token — see FR-036 … FR-041. Signing in again MUST
  remain an equally valid way to obtain a fresh pair, so a caller that already holds the password
  need not implement renewal at all.
- **FR-004**: The platform MUST refuse sign-in for unknown accounts, wrong passwords, and
  deactivated accounts, and MUST NOT disclose in the rejection which of those conditions applied.
- **FR-005**: Every endpoint in this feature other than sign-in MUST require a valid bearer token
  and MUST reject calls that present none, an expired one, or one whose signature does not verify.
- **FR-006**: The platform MUST distinguish "not authenticated" from "authenticated but not
  permitted", so a caller can tell a missing token from an insufficient permission.
- **FR-007**: Tokens MUST carry the account's identity and roles, so permission decisions on later
  calls need no second look-up of the caller's credentials.
- **FR-008**: A token MUST stop being honoured once the account behind it is deactivated, without
  waiting for its natural expiry.
- **FR-009**: Token-authenticated endpoints MUST NOT depend on browser session cookies or
  anti-forgery tokens, and MUST NOT redirect an unauthenticated caller to a sign-in page; they
  answer with a machine-readable rejection.
- **FR-010**: These endpoints MUST live at addresses distinct from the existing browser screens so
  that adding them changes no existing page behaviour.

#### Products — contract and validation delivered, data behaviour deferred

- **FR-011**: The platform MUST expose an operation returning all products, each entry carrying
  product code, primary name, English name, category, unit of measure, and active flag.
- **FR-012**: The retrieve-all operation MUST accept a way to restrict results to active products
  only, and MUST document that filter's meaning even while it selects from representative data.
- **FR-013**: The platform MUST expose an operation retrieving a single product **by its product
  code**, and the contract MUST specify a distinct "not found" response for a code that matches
  nothing.
- **FR-014**: The platform MUST expose an operation creating a product from a product code, primary
  name, and unit of measure, with English name and category optional.
- **FR-015**: The create operation MUST validate presence and length of every submitted field and
  reject violations as invalid input, naming each offending field.
- **FR-016**: The contract MUST specify a conflict response for a product code already in use,
  distinct from a validation failure.
- **FR-017**: A created product MUST be reported as active.
- **FR-018**: Creating a product MUST be restricted to callers holding the administrative
  permission; both read operations MUST be open to any authenticated caller.
- **FR-019**: Product read and create operations MUST share one product representation, so a caller
  writes a single parser.

#### Work orders — contract and validation delivered, data behaviour deferred

- **FR-020**: The platform MUST expose an operation creating a work order from a work-order number,
  a **list of input product codes**, a single **output product code**, a planned start time, and a
  quantity to manufacture.
- **FR-021**: The work-order create operation MUST accept an optional assigned machine, hourly rate,
  line setup time, and workstation capability.
- **FR-022**: The platform MUST NOT require, accept, or return internal product record numbers in
  place of product codes anywhere in this feature's contract.
- **FR-023**: The contract MUST specify the invalid-input response for an input or output product
  code that resolves to no product, and that response MUST name which code failed — for an input,
  by its **position in the submitted list**, so a caller sending several codes knows which entry to
  fix without comparing the response against its own payload.
- **FR-024**: The contract MUST specify a conflict response for a work-order number already in use.
- **FR-025**: The create operation MUST validate that the order number, the input list, the output
  code, and the planned start are present, and that the quantity is greater than zero, rejecting
  violations as invalid input.
- **FR-026**: A created work order MUST be reported in the platform's initial "ready" state; this
  feature exposes no way to start, hold, or finish an order.
- **FR-027**: The work-order response MUST echo the order including every product code — the inputs
  in the order they were submitted — so a caller can confirm what it submitted without a second
  call.
- **FR-028**: Creating a work order MUST be permitted to callers holding either the administrative
  or the client permission.

#### Contract & errors

- **FR-029**: All endpoints MUST exchange JSON and MUST answer with the response code that matches
  the outcome: success, created, invalid input, unauthorized, forbidden, not found, or conflict.
- **FR-030**: All failures MUST share one error shape carrying a human-readable message and, for
  validation failures, the offending fields — so a caller writes one error handler, not seven.
- **FR-031**: No failure response may leak internal diagnostics — stack traces, database messages,
  or record numbers.
- **FR-032**: The endpoint surface MUST be documented in a machine-readable contract that a caller
  can read without access to the source, and that contract MUST cover every response in this spec,
  including those the slice cannot yet produce.
- **FR-033**: The contract MUST state, for each operation, whether its response is drawn from real
  platform data or is representative, so no integrator mistakes sample data for plant data.
- **FR-034**: Representative responses MUST be shape-identical to the eventual real ones; wiring the
  behaviour later MUST require no change to any address, field name, field type, or response code.
- **FR-035**: Error and validation messages returned by these endpoints MUST be usable by a
  non-browser caller and MUST NOT depend on a browser's language selection to be intelligible.

#### Token renewal — delivered as working behaviour

> Added 2026-08-29 at the author's request, superseding the original FR-003. Numbered after FR-035
> so every identifier already referenced in the plan, tasks, contracts, and code keeps its meaning.

- **FR-036**: Sign-in MUST return, alongside the access token, a **refresh token** and the moment it
  expires; and the platform MUST expose an operation exchanging a refresh token for a new access
  token. That operation MUST NOT require an access token — a caller whose access token has expired
  has nothing else to present — and MUST NOT require the account's password.
- **FR-037**: A refresh token MUST outlive the access token it renews, MUST itself expire, and MUST
  be **rotated** on every renewal: each successful renewal returns a new refresh token which the
  caller stores in place of the one it presented.
- **FR-038**: Renewal MUST re-check the account against the platform and MUST be refused when the
  account no longer exists, has been deactivated, or has had its credentials rotated since the
  refresh token was issued. A refresh token MUST NOT be a way to outlive the withdrawal of access
  required by FR-008.
- **FR-039**: A renewed access token MUST carry the account's roles **as they stand at renewal**,
  not as they stood at sign-in, so a permission change takes effect within one access-token lifetime
  rather than one refresh-token lifetime.
- **FR-040**: Renewal MUST answer in the **same response shape** as sign-in, so a caller writes one
  parser and one credential-storing routine; and every renewal failure — expired, altered, unknown,
  wrong kind of token, or an account that may no longer use it — MUST be refused identically,
  disclosing which condition applied no more than sign-in does.
- **FR-041**: An access token and a refresh token MUST NOT be interchangeable: presenting a refresh
  token as a bearer credential, or an access token for renewal, MUST be refused.

#### Several inputs, one output — contract and validation delivered, data behaviour deferred

> Added 2026-09-01 at the author's request, amending FR-020, FR-023, FR-025 and FR-027 in place.
> Numbered after FR-041 so every identifier already referenced in the plan, tasks, contracts, and
> code keeps its meaning.

- **FR-042**: The work-order create operation MUST accept **one or more** input product codes and
  exactly **one** output product code. The input list MUST carry at least one entry, MUST NOT
  contain a blank entry, and MUST NOT name the same code twice under the feature's own matching rule
  (trimmed, case-insensitive); each violation MUST be rejected as invalid input naming the list.
- **FR-043**: An input MUST name only *what* is consumed. The contract MUST NOT attach a quantity,
  sequence, or any other attribute to an input in this feature; the quantity to manufacture remains
  the order's single quantity.
- **FR-044**: An output product code that repeats one of the input codes MUST be accepted — the
  no-repeats rule of FR-042 governs the input list among itself, and a rework or re-packing order
  legitimately consumes and produces the same product.

### Key Entities *(include if data involved)*

- **Account**: an existing platform user, identified by email, holding one or more roles and an
  active/inactive state. Read and authenticated by this feature; never created or modified by it.
- **Access token**: a time-limited, signed credential issued at sign-in or at renewal, valid for
  8 hours, carrying the account's identity and roles, presented on every later call.
- **Refresh token**: a longer-lived signed credential issued beside the access token, carrying the
  account's identity but **no roles**, presented only to the renewal operation and replaced by a new
  one each time it is used. As secret as the password it stands in for.
- **Product**: a material or finished good, identified externally by its **product code**, carrying
  a primary name, an optional English name, an optional category, a unit of measure, and an active
  flag. Defined here as a contract shape; not yet read from or written to platform data.
- **Work order**: a production task, identified by its work-order number, referencing **one or more
  input products** and a single output product **by code**, with a planned start, a quantity to
  manufacture, an optional assigned machine, optional rate/setup/capability figures, and a lifecycle
  status that begins at "ready". Defined here as a contract shape; not yet persisted.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An integration developer with only the published contract and a set of credentials can
  sign in and exercise all six operations end to end without reading source code or asking a
  question.
- **SC-002**: 100% of the six operations — sign in, renew, list products, fetch one product, create a
  product, create a work order — are reachable by a non-browser caller carrying nothing but a token.
- **SC-010**: An unattended caller can stay authenticated for the full refresh-token lifetime
  without the account's password appearing anywhere in its configuration, and loses that access
  within one request of the account being deactivated.
- **SC-003**: Every rejection in the acceptance scenarios above is distinguishable by its response
  code alone, without parsing message text: invalid input, unauthorized, forbidden, not found, and
  conflict never share a code.
- **SC-004**: No operation in this feature requires the caller to know or supply an internal record
  number for a product; product codes suffice in 100% of cases.
- **SC-005**: When the deferred behaviour is wired in a later slice, a client built against this
  contract continues to work with **zero** changes to addresses, field names, field types, or
  response codes.
- **SC-006**: Every operation whose response is representative rather than real is labelled as such
  in the published contract — 100% coverage, so no integrator can mistake one for the other.
- **SC-007**: A caller receives an answer to any of these operations within 2 seconds under normal
  plant load.
- **SC-008**: Adding this surface changes no existing browser screen behaviour: every existing page
  and permission continues to work exactly as before.
- **SC-009**: 100% of failure responses share the single documented error shape, and none contains
  internal diagnostics.

## Assumptions

- **This slice is contract-only for the five business operations**, per the requester's "just apis
  without logic for now". Authentication, permissions, and request-shape validation are genuinely
  implemented because a contract that cannot be called under realistic credentials cannot be
  meaningfully tested; the product and work-order operations answer with representative data.
- **Existing accounts and roles are reused.** This feature authenticates against the platform's
  existing users and the existing administrative and client roles; it creates no new role and no
  separate credential store.
- **Permissions mirror the existing browser screens.** Managing products stays administrative;
  raising a work order is open to administrative and client callers; reading the catalogue is open
  to any authenticated caller. This keeps one permission model rather than two that can drift.
- **An access token lasts 8 hours; a refresh token lasts 30 days** — one plant shift per access
  token, one comfortable month per refresh token. Both are configurable; neither is unlimited.
- **Refresh tokens are stateless, like access tokens.** This slice adds no schema (see below), so
  there is no table of issued refresh tokens and therefore no per-token revocation list. The
  consequences are stated plainly in the contract: rotation advances the credential but does not
  invalidate the one it replaced, and what revokes a leaked refresh token immediately is
  deactivating the account or changing its password — both of which this feature already honours on
  the next request (FR-008, FR-038). A stored, individually revocable refresh token is a later
  change if a real threat model asks for one.
- **The two token kinds are kept apart by construction**, not by convention, so that neither can be
  used in the other's place (FR-041).
- **Product codes are matched case-insensitively with surrounding whitespace trimmed.** The rule is
  fixed now and stated in the contract even though nothing matches against real data yet, so the
  behaviour wired later cannot surprise a client already built against it.
- **No pagination in this slice.** The product catalogue is small enough (hundreds, not millions)
  that the list operation returns it whole. Pagination is a later change if volume demands it.
- **No schema change in this slice — with one field the platform will have to grow into.** Every
  field these endpoints exchange already exists on the platform's products, work orders, and user
  records, with a single exception introduced by the 2026-09-01 amendment: a work order stores one
  input product, and the contract now names several. This slice persists nothing, so it still adds
  no migration; the follow-up behaviour slice carries one join table for the additional inputs. The
  cost is stated here rather than discovered later (research R13).
- **The bilingual product names are both returned** — primary and English travel in every product
  response, and the caller chooses which to display.

## Out of Scope

- Reading or writing real product and work-order data through these endpoints — deferred to the
  follow-up slice that wires the behaviour behind this contract.
- Updating or deactivating a product through the API.
- Advancing a work order's lifecycle (start, hold, resume, finish) or recording its inputs.
- Reading machines, telemetry, dashboards, or OEE figures.
- Creating, editing, or deactivating user accounts, and changing passwords.
- Server-side storage of refresh tokens and per-token revocation lists — renewal is stateless in
  this slice; see Assumptions.
- API keys, rate limiting, quotas, and caller-specific throttling.
- A sign-out endpoint that revokes an outstanding token pair on demand; deactivating the account or
  changing its password is what does that today.
- Any change to the existing browser screens or their cookie-based sign-in.
