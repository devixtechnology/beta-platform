# Research & Decisions: Token-Authenticated Integration API

**Feature**: `005-jwt-integration-api` | **Date**: 2026-08-28 | **Phase**: 0

Every unknown in the plan's Technical Context is resolved below. The recurring tension in this
feature is that the app is already an authenticated, cookie-based MVC monolith, and we are adding a
second, stateless way in without disturbing the first.

---

## R1 — Bearer tokens alongside the existing cookie sign-in

**Decision**: Register the JWT bearer handler as an **additional, non-default** authentication
scheme. Identity's cookie scheme stays the default; every API controller declares
`[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.

**Rationale**: `AddIdentity<ApplicationUser, IdentityRole>()` in `Program.cs` sets
`DefaultAuthenticateScheme`, `DefaultChallengeScheme`, and `DefaultSignInScheme` to
`Identity.Application`. Calling `AddAuthentication(...)` afterwards with a new default would
re-point those and break every existing browser screen — a violation of FR-010 and SC-008. Adding
the handler with `builder.Services.AddAuthentication().AddJwtBearer(...)` leaves the Identity
defaults untouched and simply teaches the app a second scheme it uses only when asked.

Naming the scheme explicitly on the controller is what makes the challenge behaviour correct: an
unauthenticated API call is challenged by the **bearer** handler (a bare `401`), never by the cookie
handler (a `302` to `/Auth/Login`). This is the whole of FR-009 and needs no custom middleware, no
path-based branching, and no `MapWhen`.

**Alternatives considered**:
- *Policy scheme / scheme selector that sniffs the `Authorization` header* — a common recipe, but it
  exists to let one endpoint accept either credential. No endpoint here does; each surface has
  exactly one. Rejected as machinery for a problem we do not have (Principle III).
- *Path-based cookie-option branching* (`options.Events.OnRedirectToLogin` inspecting
  `/api`) — makes the cookie handler responsible for knowing about the API, which is backwards, and
  leaves the redirect one forgotten path away from reappearing.
- *A separate ASP.NET Core application for the API* — rejected outright by Principle I.

---

## R2 — Telling "no token" apart from "wrong role" (FR-006)

**Decision**: Rely on the framework's built-in split — the bearer handler's *challenge* produces
`401 Unauthorized`, its *forbid* produces `403 Forbidden` — and add a
`JwtBearerEvents.OnChallenge` handler only to write the standard error body (R5) instead of an
empty response.

**Rationale**: The distinction FR-006 asks for is already exactly how `[Authorize]` behaves once the
scheme is stated: no/invalid token → challenge → 401; valid token whose roles fail the policy →
forbid → 403. The only gap is that both default to an empty body, which conflicts with FR-030's
single error shape. Writing the body in `OnChallenge`/`OnForbidden` closes that gap without touching
the decision logic.

`OnChallenge` will also surface *why* a token was refused in the `WWW-Authenticate` header
(`invalid_token`, `error_description="The token is expired"`), which is what lets an integration
tell an expired token from a malformed one and re-sign-in automatically rather than page an operator
— the first Edge Case in the spec.

**Alternatives considered**: A custom `IAuthorizationMiddlewareResultHandler` — more surface area to
achieve what two event hooks already do.

---

## R3 — A token must die when the account is deactivated (FR-008)

**Decision**: Stamp the user's Identity **security stamp** into the token as a claim at sign-in, and
re-validate it on every request in `JwtBearerEvents.OnTokenValidated`: load the user, reject if the
user is missing, if `IsActive` is false, or if the stored security stamp no longer matches the claim.

**Rationale**: This is the hard requirement in an otherwise stateless design, and the reason the
"contract-only" slice still touches the database. Feature 004 already deactivates an account by
rotating its security stamp, and `Program.cs` already re-checks cookies against that stamp every
minute via `SecurityStampValidatorOptions`. Mirroring the same mechanism for tokens means
deactivation has **one** meaning across both doors into the app, rather than a cookie rule and a
divergent token rule that drift apart the first time someone changes one.

Cost is one primary-key lookup per API request. At this feature's scale (a handful of integration
callers, SC-007's 2-second budget) that is not a consideration, and it buys immediate revocation
instead of an 8-hour window in which a withdrawn account still works.

**Alternatives considered**:
- *Accept the 8-hour window* — rejected: it contradicts FR-008, and "access withdrawn" that takes
  until tomorrow morning to bite is not access withdrawal.
- *A denylist of revoked token identifiers* — needs a store, an expiry sweep, and a migration, and
  duplicates a revocation signal the security stamp already provides. Rejected by Principle III and
  by the no-schema-change constraint.
- *Short tokens plus refresh* — as a **substitute** for the stamp check this stays rejected: it only
  shrinks the revocation window rather than closing it. (Renewal was later added for a different
  reason — keeping an unattended caller alive without storing a password — and it does **not**
  replace this check. R12 applies the very same predicate at renewal, so the window stays closed on
  both paths.)

---

## R4 — Signing key, issuer, audience, and Production safety

**Decision**: A `Jwt` configuration section — `Issuer`, `Audience`, `SigningKey`, `LifetimeHours`
(default `8`) and `RefreshLifetimeDays` (default `30`, added by R12). Validate issuer, audience,
lifetime, and signature, with `ClockSkew` reduced to `TimeSpan.Zero`. At startup, **fail fast** if the signing key is absent or shorter than 32 bytes;
in Production, additionally refuse to start on a key that matches the development placeholder.

**Rationale**: Feature 004 established the precedent that a convenience default which is safe in
development must be impossible in Production — that is exactly why `DbSeeder` refuses a default
admin password there. A signing key is the same class of secret with a worse blast radius: whoever
holds it mints tokens for any account and any role. The check belongs at startup, where it is loud
and unmissable, not at first sign-in, where it surfaces as a puzzling runtime failure.

The 32-byte floor is not arbitrary: HMAC-SHA256 keys shorter than the hash output are rejected by
the token library at runtime with an error that reads as a configuration mystery. Catching it at
startup turns that into a sentence.

Default `ClockSkew` is five minutes, which would silently extend every 8-hour token to 8h05m and
make FR-002 untestable to the minute. Setting it to zero makes expiry mean what the spec says.

`appsettings.Development.json` carries a development-only key; `appsettings.json` carries the
section with an **empty** `SigningKey` so the shape is discoverable without shipping a usable
secret.

The same fail-fast net covers the lifetimes: both must be positive, and the refresh lifetime must
exceed the access lifetime. Configured the other way round, every renewal would fail after the first
eight hours, and the symptom would read as an intermittent `401` rather than as a settings mistake
(R12).

**Alternatives considered**: Asymmetric RS256 with a certificate — appropriate when a third party
must verify tokens without the ability to mint them. Here the issuer and the verifier are the same
process, so symmetric HS256 is the simpler correct answer.

---

## R5 — One error shape (FR-030, FR-031)

**Decision**: Use RFC 9457 **`ProblemDetails`** as the single error shape, with
`ValidationProblemDetails` (its `errors` dictionary) for field-level failures. Register
`AddProblemDetails()` and an exception handler so unhandled faults also emerge in that shape.

**Rationale**: FR-030 asks for one shape carrying a message and, for validation failures, the
offending fields. That is the definition of `ValidationProblemDetails`. It is already what
`[ApiController]` returns for a failed `ModelState`, so choosing it means the automatic path and the
hand-written path agree by default — the alternative is a custom envelope plus a
`InvalidModelStateResponseFactory` to force the automatic path into it, which is more code to reach
a less standard result.

It also directly serves FR-031: `ProblemDetails` has no field in which a stack trace or a database
message can travel by accident. The exception handler maps anything unhandled to a bare
`500 ProblemDetails`, so a leak requires someone to add one deliberately.

**Alternatives considered**: A bespoke `{ success, message, errors[] }` envelope — familiar to some
integrators, but non-standard, unsupported by generated clients, and it would fight `[ApiController]`
rather than ride it.

---

## R6 — Publishing a machine-readable contract (FR-032)

**Decision**: `Microsoft.AspNetCore.OpenApi` (first-party, .NET 9) serving the generated document at
`/openapi/v1.json`, **plus `Scalar.AspNetCore` rendering an interactive reference at `/docs`**.

> **Revised during implementation (2026-08-29), at the author's request.** The original decision was
> document-only, on the grounds that FR-032 asks for a machine-readable contract and Principle III
> does not fund a convenience. The author asked for a Swagger-style page, which settles it: an
> interactive console is what their integrators expect. Scalar was chosen over Swashbuckle because it
> *renders the existing document* rather than bringing a second schema generator — so there is still
> exactly one source of truth, and the page cannot drift from the contract. It also serves its own
> JavaScript bundle from the application's origin (verified: the page references no external host),
> which matters on a plant network that may not reach a CDN.

XML doc comments turned out **not** to reach the document: `Microsoft.AspNetCore.OpenApi` in .NET 9
does not read them. Per-operation text therefore comes from `[EndpointSummary]` /
`[EndpointDescription]` on the actions, and the document-level slice note and bearer security scheme
from an `IOpenApiDocumentTransformer`. Without the transformer the document described the endpoints
and no way to authenticate against any of them.

**Rationale**: FR-032 asks for a machine-readable contract a caller can read without the source. The
first-party package produces exactly that with `AddOpenApi()` + `MapOpenApi()` and one new
dependency from the same vendor as the framework. Swashbuckle would add an interactive page, but
also a third-party dependency, its own configuration surface, and a version-compatibility question
at every .NET upgrade — for a contract that this feature *also* ships as hand-written markdown in
`contracts/` and as runnable examples in `quickstart.md`. SC-001 is met by document plus examples;
the UI is a convenience Principle III does not fund.

FR-033 (label which responses are representative) is met by putting that sentence in each affected
operation's summary, so it travels inside the published document rather than in a side note an
integrator never reads.

**Alternatives considered**: Swashbuckle.AspNetCore — its UI is the familiar one, but it also
carries its own schema generator, which would mean two descriptions of the same API that can
disagree; hand-maintaining an OpenAPI file only (drifts from the code the first time anyone edits a
DTO).

---

## R7 — Keeping the contract stable across the behaviour slice (FR-034, SC-005)

**Decision**: Controllers depend on two new abstractions, `IProductApiService` and
`IWorkOrderApiService`, whose methods speak in API DTOs and return a small result type carrying an
outcome (`Ok` / `NotFound` / `Conflict` / `Invalid`). This slice registers `SampleProductApiService`
and `SampleWorkOrderApiService`. The follow-up slice registers real implementations that delegate to
the existing `IProductService` / `IWorkOrderService`, changing two lines in `Program.cs` and no
controller, DTO, route, or status code.

**Rationale**: This is the seam the author's Q1 answer requires. Without it, "wire the behaviour
later" means editing controllers — and controllers are where routes, status codes, and response
shapes live, which is precisely what FR-034 and SC-005 forbid touching. With it, the swap is a DI
registration.

The outcome-carrying result type matters as much as the interface: if the sample service could only
return data, the controller would have to invent the `404`/`409` paths itself later, i.e. change.
Returning an outcome now means the controller already contains its full response map — including the
branches this slice cannot yet reach — and those branches are what US2 §5, US3 §5 and US4 §7 assert
are specified today.

**Principle III note**: two interfaces whose only implementations today return canned data is
exactly the shape of speculative abstraction the constitution warns about, so it is logged in the
plan's Complexity Tracking. The justification is that the requirement itself is the seam — the
author asked for a contract that outlives its stub — not a guess about a future need. The existing
`ServiceResult<T>` is reused rather than a new result type invented, keeping the addition to two
interfaces and two classes.

**Alternatives considered**:
- *Controllers returning canned DTOs inline* — fewest types today, but every deferred behaviour then
  lands in a controller, breaking Principle V and SC-005 together.
- *API controllers depending directly on the existing `IProductService`/`IWorkOrderService`* — those
  return entities and hit the database, which is the behaviour this slice defers; it would also push
  entity-to-DTO mapping and code-to-id resolution into controllers.

---

## R8 — Routing, and the global fallback policy

**Decision**: Attribute-routed controllers under `Controllers/Api/`, rooted at `api/v1/…`, marked
`[ApiController]`. The sign-in endpoint is `[AllowAnonymous]`; everything else inherits the JWT
`[Authorize]`. Route registration order is unchanged, and no MVC convention is touched.

**Rationale**: `Program.cs` maps one conventional route, `{controller=Dashboard}/{action=Index}/{id?}`.
An attribute-routed controller is matched by its own template and never by that convention, so the
API cannot shadow or be shadowed by an existing screen (FR-010, SC-008). The `api/v1` prefix means
the eventual v2 is an additive folder rather than a negotiation.

The global `FallbackPolicy` (`RequireAuthenticatedUser`) already in `AddAuthorization` applies to the
new controllers too — which is the desired default — but it authenticates against the *default*
(cookie) scheme, so the explicit `[Authorize(AuthenticationSchemes = …)]` on each API controller is
what actually governs. The sign-in endpoint must therefore be `[AllowAnonymous]` or the fallback
policy will lock the door and leave the key inside.

`[ApiController]` is what turns a failed `ModelState` into a `400 ValidationProblemDetails`
automatically (R5) and makes `[FromBody]` binding implicit.

**Alternatives considered**: A URL segment version (`/api/products`, unversioned) — cheaper now,
awkward the first time a field changes shape. Header-based versioning via
`Asp.Versioning.*` — a dependency for a decision one path segment already expresses.

---

## R9 — Product code normalisation (spec Assumptions)

**Decision**: Trim surrounding whitespace and compare case-insensitively. Implement it now as a
single shared helper used by the sample services and by the eventual real ones, and state the rule
in the published contract.

**Rationale**: The spec fixes this rule now precisely so that a client built against it cannot be
surprised later. The risk it guards against is subtle: MySQL's default collation
(`utf8mb4_general_ci` / `_0900_ai_ci`) is already case-insensitive, so a real implementation written
without thinking would *accidentally* match case-insensitively on the server while any in-memory or
client-side check matched case-sensitively. Writing the rule down and centralising it means the two
halves agree deliberately rather than by luck of collation.

Trimming is applied on the way in, so a code stored via the API is stored in the form the plant
typed, minus accidental padding.

**Alternatives considered**: Case-sensitive exact match — rejected: the plant treats a code as a
printed label, and it would make the API stricter than the browser screens for the same data.

---

## R10 — Message language for API callers (FR-035)

**Decision**: API error and validation messages are English literals, produced independently of the
request-localisation middleware. `IStringLocalizer` and the `.resx` files are not used on this
surface.

**Rationale**: The app's `RequestCultureProvider` list contains only `CookieRequestCultureProvider`.
An API caller sends no culture cookie, so every request would fall to the default culture anyway —
the localisation machinery would run and decide nothing. Worse, a machine caller matching on message
text would be silently at the mercy of a cookie it never sets.

FR-035 is met by keeping messages stable and culture-independent, and SC-003 ensures a caller never
has to read the text to branch: the status code carries the outcome.

**Alternatives considered**: `Accept-Language` negotiation — a real feature, but nothing in the spec
asks for it and no caller exists to want it.

---

## R11 — What "tested" means for a contract-only slice

**Decision**: Unit-test the parts that are real — token creation and its claims, the security-stamp
revocation predicate, product-code normalisation, request validation rules, and the controllers'
mapping from a service outcome to a status code — in the existing `tests/BetaPlatform.Tests` xUnit
project. Do not stand up an in-process HTTP host.

**Rationale**: The constitution asks for business logic covered by unit tests that run without the
web host, and the existing suite (`ProductServiceTests`, `WorkOrderServiceTests`, …) already works
that way against `Microsoft.EntityFrameworkCore.InMemory`. Everything genuinely decided in this
slice is reachable that way: the outcome-to-status map is a function of the result type, and token
issuing is a service.

The one thing unit tests cannot prove is R1's scheme wiring — that an unauthenticated API call
returns `401` rather than a redirect to `/Auth/Login`. That is a composition-root behaviour, and it
is the single most likely thing to be got wrong. It is therefore covered explicitly by the
`quickstart.md` manual checks rather than left to inference.

**Alternatives considered**: `Microsoft.AspNetCore.Mvc.Testing` + `WebApplicationFactory` for real
end-to-end assertions — genuinely the right tool for R1, but it pulls a new test dependency and a
running MySQL or a test-host database substitution to get past `DbSeeder` at startup. Deferred to
the slice that wires real behaviour, where there will be much more worth asserting end to end.

---

## R12 — Renewal without a refresh-token table (FR-036 … FR-041)

> **Added 2026-08-29, at the author's request.** The original FR-003 refused a refresh credential
> outright, on the grounds that signing in again is free when you hold the password. The author asked
> for a refresh endpoint, which settles it: their integrators expect one, and an unattended caller
> that renews does not need the account's password in its configuration at all — which is a real
> security gain, not only a convenience.

**Decision**: The refresh token is a **second signed JWT**, minted at sign-in beside the access
token, with a longer lifetime (`Jwt:RefreshLifetimeDays`, default 30), a **different audience**
(`{Audience}.refresh`), a `token_use` claim, the security stamp, and **no role claims**.
`POST /api/v1/auth/refresh` is anonymous, takes the token in the body, verifies it, re-reads the
account, and returns a **new pair** in the sign-in response shape. **No table, no migration.**

**Rationale**, point by point, because each choice is load-bearing:

*A JWT rather than an opaque random string.* An opaque token means a store: a table, an index, an
expiry sweep, a migration — and 005's hard constraint is that it changes no schema (constitution
Principle IV, plan §Storage). A signed token carries its own expiry and its own subject, so the
platform can verify it with the key it already holds. The cost is honestly stated in the contract:
without a store there is no per-token revocation list, so rotation advances the credential but does
not invalidate its predecessor. What *does* revoke immediately is the security stamp — which is
exactly the lever an administrator already pulls to deactivate an account or change a password.

*A different audience.* This is what makes the two kinds non-interchangeable (FR-041) **by
construction** rather than by a check someone can forget. The bearer handler validates
`Jwt:Audience`; a refresh token carries `Jwt:Audience + ".refresh"` and is rejected before a single
claim is read. The renewal endpoint validates the opposite. A `token_use` claim is checked in both
places on top of that — not because the audience check is insufficient, but because a decoded token
should *say* what it is, and because the explicit check survives someone loosening an audience rule
later. Two lines for a property that would otherwise depend on nobody making a mistake.

*No roles in the refresh token.* A role claim inside a 30-day credential outlives its own withdrawal
by up to a month. Roles are re-read from the store at every renewal (FR-039), so a permission change
bites within one **access**-token lifetime. This is also why the refresh token is thinner in general:
it proves *who*, and nothing about *what may be done*.

*The same revocation predicate at renewal.* `ApiTokenValidation.IsStillValid` — R3's function — is
applied to the account behind a presented refresh token, unchanged. If renewal used a different rule,
the two would drift, and the first drift would be a deactivated account renewing its way back in
(FR-038). One predicate, two call sites.

*Rotation.* Returning a new refresh token on every renewal keeps the credential moving, so one
captured from a single exchange goes stale as the legitimate caller advances. With no ledger it is
not a detection mechanism, and the contract says so rather than implying a guarantee it cannot make.

*Anonymous, and in the body.* Requiring a valid access token to renew would defeat the purpose — the
caller renews precisely because its access token has expired. In the body rather than a header or
query string so the credential cannot be captured by an access log or a browser history.

*A startup rule that the refresh lifetime exceeds the access lifetime.* Configured the other way
round, every renewal would fail after the first eight hours and the symptom would read as an
intermittent `401` rather than as the configuration mistake it is. Same reasoning as R4's key checks:
catch it where it is loud.

**Alternatives considered**:
- *Opaque token stored in a new `refresh_tokens` table, individually revocable* — the textbook
  answer, and the right one if a threat model demands per-token revocation. It needs an entity, a
  migration, and an expiry sweep, which is precisely the scope 005 excludes. Logged in the spec's
  Assumptions as a later change rather than pretended away.
- *A `typ` header value instead of a separate audience* — carried by the same JWT header the library
  already validates loosely; an audience is what `TokenValidationParameters` checks by default, so
  it fails safe with no extra code.
- *Sliding expiry on the access token instead of a refresh token* — a token that renews itself on
  use has no fixed lifetime, which contradicts FR-002 and makes the revocation story harder, not
  easier.
- *Refresh token as a long-lived API key* — never expiring is the property that makes a leak
  permanent. Rejected outright.

---

## R13 — Several input products, one output (FR-042 … FR-044)

> **Added 2026-09-01, at the author's request.** The original contract gave a work order one input
> code and one output code, mirroring the `work_orders` row. The author asked for the input side to
> be a list. That is the plant's shape, not a convenience: an order consumes steel *and* paint *and*
> fixings, and produces one end product.

**Decision**: `inputProductCodes` is an **array of product codes** — at least one entry, no blank
entry, no code twice — and `outputProductCode` stays a **single** code. No quantity, sequence, or
other attribute is attached to an input. An unresolvable input names its **position**,
`inputProductCodes[i]`, in the validation errors.

**Rationale**, point by point:

*The output stays singular.* Pluralising both sides would look symmetrical and be wrong: an order
produces one end product, and every caller would spend forever unwrapping a one-element array. The
asymmetry belongs to the domain, so the contract states it rather than smoothing it over.

*No quantity per input.* A `{ productCode, qtyRequired }` object was the obvious alternative and is
the shape a bill of materials eventually needs. It is declined here because nothing in this feature —
or in `work_orders` — has anywhere to put a per-input quantity, and a field the platform silently
ignores is worse than a field that is missing: the first is a lie the contract tells, the second is
scope a later feature can add without breaking a caller. Adding a member to an array element is a
compatible change later; removing one is not.

*No code twice.* Because an input carries no quantity, a repeated code says nothing a single entry
does not. Silently collapsing it would hide a client that built its list wrongly, so it is refused at
the edge, compared through the same `ProductCode` helper as everything else (R9) — "rm-01" and
" RM-01 " are repeats.

*An empty list is refused.* `[]` is the same class of mistake as `qtyToManufacture: 0`, which this
feature already rejects rather than accepting an order to manufacture nothing.

*No ceiling on the list.* The same reasoning as `GreaterThanZeroAttribute`: a limit invented to
satisfy a validator eventually refuses a legitimate order. The request-size limits ASP.NET Core
already applies are a real bound; a made-up one is not.

*The error names the index.* FR-023 required naming which of two codes failed. With a list, that
means the position — a caller sending six codes must not have to diff the response against its own
payload. `inputProductCodes[1]` is the same key ASP.NET Core model binding produces for an element of
a bound collection, so the shape is the framework's, not an invention of ours.

**What this costs, stated plainly**: `work_orders` holds a single `input_product_id`, so the contract
now describes something the schema cannot yet store. It costs *this* slice nothing — 005 persists
nothing and still adds no migration — but the follow-up behaviour slice must add a
`work_order_input_products` join table (`work_order_id`, `product_id`, unique together) via an EF
Core migration, where before it needed none. That is a genuine change to the follow-up's scope and is
recorded in `spec.md` Assumptions, `data-model.md`, and `tasks.md` rather than left to be discovered.

**Alternatives considered**:
- *Reuse the existing `work_order_inputs` table* — it is the 003 change request's manually-recorded
  input **weights**, carrying no product reference at all. Two different concepts that happen to
  share a word; overloading it would corrupt both.
- *Keep one input and let a caller raise several orders* — one order per material misrepresents a
  single production task as many, and every downstream count (orders raised, uptime per order) would
  be wrong for the sake of avoiding a join table.
- *Add `inputProductCode2`, `inputProductCode3` … columns* — a fixed ceiling in the schema, chosen
  arbitrarily, and a contract that grows a new field name every time the plant changes a recipe.
- *Accept both `inputProductCode` and `inputProductCodes` for compatibility* — nothing consumes this
  API yet (the slice persists nothing and has no integrator in production), so there is no
  compatibility to keep; two ways to say the same thing would be permanent, and the ambiguity of
  sending both would need its own rule.

---

## Resolved: no open questions

The two decisions the specification deferred were answered by the author before planning began and
are recorded in `spec.md` (contract-only for the five business operations; 8-hour tokens). The third,
raised after implementation, is recorded in R12: tokens are renewed by a refresh token as well as by
signing in again. The fourth, raised on 2026-09-01, is recorded in R13: a work order names several
input products and one output. No `NEEDS CLARIFICATION` remains in the Technical Context.
