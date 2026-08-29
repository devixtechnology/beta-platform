---
description: "Task list for 005-jwt-integration-api"
---

# Tasks: Token-Authenticated Integration API (Products & Work Orders)

**Input**: Design documents from `/specs/005-jwt-integration-api/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: **Included.** Not a default — the constitution asks that business logic be covered by
unit tests that run without the web host, and research R11 fixes what that means for a contract-only
slice: test the parts that are genuinely real (token claims, the revocation predicate, code
normalisation, request validation, outcome→status mapping) in the existing
`tests/BetaPlatform.Tests` xUnit project. No new test dependency, no in-process HTTP host.

**Organization**: Grouped by user story so each is independently implementable and verifiable.

> **Amended 2026-08-29.** Token renewal was added after Phase 7 completed, superseding the original
> FR-003. Its tasks are Phase 8 (T050–T059) at the end of this file rather than folded into Phase 3,
> so the record of what was built when stays honest.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel — different file, no dependency on incomplete work
- **[Story]**: US1–US4, mapping to the user stories in spec.md

## Path Conventions

Single ASP.NET Core project (constitution Principle I): `src/BetaPlatform/`, tests in
`tests/BetaPlatform.Tests/`. New code is confined to `Controllers/Api/`, `Services/Api/`, and
`ViewModels/Api/`.

> ⚠️ **`Program.cs` is a single file.** Tasks T010–T014 all edit it and therefore **cannot** run in
> parallel with each other, even though they are separate concerns. This is the main serialisation
> point in the plan.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Dependencies and configuration in place before any code is written

- [X] T001 Add `Microsoft.AspNetCore.Authentication.JwtBearer` 9.0.0 and `Microsoft.AspNetCore.OpenApi` 9.0.0 package references, and set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` with `<NoWarn>$(NoWarn);1591</NoWarn>`, in `src/BetaPlatform/BetaPlatform.csproj`
- [X] T002 [P] Add the `Jwt` section (`Issuer`, `Audience`, `SigningKey` **empty**, `LifetimeHours: 8`) to `src/BetaPlatform/appsettings.json` — the shape is discoverable, the secret is not shipped
- [X] T003 [P] Add a development-only `Jwt:SigningKey` of at least 32 bytes to `src/BetaPlatform/appsettings.Development.json`
- [X] T004 Run `dotnet build` and confirm the two new packages restore against the pinned .NET 9 framework version

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The authentication and cross-cutting plumbing every user story sits on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T005 [P] Create `JwtOptions` (Issuer, Audience, SigningKey, LifetimeHours defaulting to 8) with a `SectionName` constant in `src/BetaPlatform/Services/Api/JwtOptions.cs`, following the existing `TelemetryOptions` pattern
- [X] T006 [P] Create the `ProductCode` helper — `Normalise(code)` trimming whitespace and `Matches(a, b)` comparing `OrdinalIgnoreCase` — in `src/BetaPlatform/Services/Api/ProductCode.cs` (research R9)
- [X] T007 [P] Create the pure revocation predicate `ApiTokenValidation.IsStillValid(ApplicationUser? user, string? securityStampClaim)` — false when the user is null, `IsActive` is false, or the stamp does not match — in `src/BetaPlatform/Services/Api/ApiTokenValidation.cs`. Kept a pure function so it is unit-testable without a host (research R11)
- [X] T008 Create `IJwtTokenService` / `JwtTokenService` issuing an HS256 token with claims `sub`, `email`, `name`, one `role` per role, `AspNet.Identity.SecurityStamp`, `jti`, `exp`/`nbf`/`iat`/`iss`/`aud`, returning the token and its absolute UTC `expiresAt`, in `src/BetaPlatform/Services/Api/JwtTokenService.cs` (depends on T005; claim list in data-model.md)
- [X] T009 Create `JwtBearerEventHandlers` in `src/BetaPlatform/Services/Api/JwtBearerEventHandlers.cs`: `OnTokenValidated` loading the caller via `UserManager` and failing the token when `ApiTokenValidation.IsStillValid` is false; `OnChallenge` and `OnForbidden` writing `ProblemDetails` bodies and setting `WWW-Authenticate` so an expired token is distinguishable from a malformed one (depends on T007; see contracts/errors.md)
- [X] T010 In `src/BetaPlatform/Program.cs`, bind the `Jwt` section to `JwtOptions` and **fail fast at startup** when the signing key is missing or shorter than 32 bytes, and in Production also when it equals the development placeholder (research R4, mirroring 004's no-default-admin-password rule)
- [X] T011 In `src/BetaPlatform/Program.cs`, register JWT bearer via `AddAuthentication().AddJwtBearer(...)` — **without** changing any default scheme, so Identity's cookie scheme stays default — with `TokenValidationParameters` validating issuer, audience, lifetime and signature and setting `ClockSkew = TimeSpan.Zero` (research R1, R4). ⚠️ Do **not** write `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)`; that re-points the Identity defaults and breaks every browser screen
- [X] T012 In `src/BetaPlatform/Program.cs`, wire `JwtBearerEventHandlers` into the `AddJwtBearer` options' `Events` (depends on T009, T011)
- [X] T013 In `src/BetaPlatform/Program.cs`, add `AddProblemDetails()` and an exception handler so unhandled faults return a bare `500 ProblemDetails` carrying no stack trace or database message (FR-031, research R5)
- [X] T014 In `src/BetaPlatform/Program.cs`, add `AddOpenApi()` and `MapOpenApi()` serving `/openapi/v1.json`, and register `IJwtTokenService` in DI (research R6)

**Checkpoint**: The app builds and starts, refuses to start without a signing key, and publishes an
(empty) OpenAPI document. No endpoint exists yet.

---

## Phase 3: User Story 1 - Obtain an access token (Priority: P1) 🎯 MVP

**Goal**: A caller signs in with real credentials and receives a real 8-hour token. Fully
implemented — nothing representative here.

**Independent Test**: Sign in with valid credentials and get a token whose `expiresAt` is ~8 hours
out; confirm wrong password, unknown email, and a deactivated account all return an identical `401`;
confirm a missing field returns `400`.

- [X] T015 [P] [US1] Create `LoginRequest` (`email` required + email format, `password` required) in `src/BetaPlatform/ViewModels/Api/LoginRequest.cs`. Do **not** apply the password-policy length rule — a short password is a wrong password (`401`), not a malformed request (`400`), and splitting them would confirm the policy exists (data-model.md)
- [X] T016 [P] [US1] Create `LoginResponse` (`accessToken`, `tokenType`, `expiresAt`, `email`, `fullName`, `roles[]`) in `src/BetaPlatform/ViewModels/Api/LoginResponse.cs`
- [X] T017 [US1] Create `AuthApiController` with `[ApiController]`, `[Route("api/v1/auth")]`, `[AllowAnonymous]` and a `POST login` action in `src/BetaPlatform/Controllers/Api/AuthApiController.cs`: look the user up, reject an inactive account, verify the password via `UserManager`/`SignInManager`, load roles, and issue the token. Unknown user, wrong password, and deactivated account MUST all return the **same** `401 "Invalid credentials."` (FR-004). `[AllowAnonymous]` is mandatory — the global `FallbackPolicy` would otherwise lock the door and leave the key inside (research R8)
- [X] T018 [P] [US1] Unit-test token issuing in `tests/BetaPlatform.Tests/JwtTokenServiceTests.cs`: every claim in the data-model.md table is present, `expiresAt` is issue + `LifetimeHours`, one `role` claim per role, and the security stamp is carried
- [X] T019 [P] [US1] Unit-test the revocation predicate in `tests/BetaPlatform.Tests/ApiTokenValidationTests.cs`: valid user passes; null user, `IsActive = false`, and a rotated stamp each fail
- [X] T020 [US1] Run quickstart checks 1, 2 and 4 from `specs/005-jwt-integration-api/quickstart.md`. **Check 1 is the important one**: an unauthenticated `GET /api/v1/products` must return `401`, never a `302` to `/Auth/Login`. Unit tests cannot prove this, and it is the single most likely thing to be got wrong

**Checkpoint**: Sign-in works end to end. A token can now be obtained for testing every other story.

---

## Phase 4: User Story 2 - Read the product catalogue (Priority: P2)

**Goal**: List products and fetch one by code, in the final shape, from representative data.

**Independent Test**: With a token, list products and confirm each entry carries code, both names,
category, unit and active flag and **no product id**; fetch one by code and confirm
`rm-steel-01`, `RM-STEEL-01` and `  RM-Steel-01  ` all reach the same product; call without a token
and get `401`.

- [X] T021 [P] [US2] Create `ProductResponse` (`productCode`, `productName`, `productNameEnglish`, `category`, `unit`, `isActive`) in `src/BetaPlatform/ViewModels/Api/ProductResponse.cs`. **No `productId` field** — its absence is FR-022 and the central promise of the feature
- [X] T022 [US2] Create `IProductApiService` with `GetAllAsync(bool activeOnly)` and `GetByCodeAsync(string productCode)` returning `ServiceResult`-carried outcomes so the controller can already distinguish found from not-found, in `src/BetaPlatform/Services/Api/IProductApiService.cs` (research R7 — this interface is the seam the behaviour slice swaps)
- [X] T023 [US2] Create `SampleProductApiService` returning a small fixed catalogue (include one inactive product and one Arabic name) and resolving codes through `ProductCode.Matches`, in `src/BetaPlatform/Services/Api/SampleProductApiService.cs`; register it in `src/BetaPlatform/Program.cs`
- [X] T024 [US2] Create `ProductsApiController` with `[ApiController]`, `[Route("api/v1/products")]` and `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`, exposing `GET /` (with the `activeOnly` query parameter) and `GET /{productCode}`, in `src/BetaPlatform/Controllers/Api/ProductsApiController.cs`. Map the not-found outcome to `404` **now** — the branch must exist even though the sample service cannot yet reach it (FR-034)
- [X] T025 [P] [US2] Unit-test code normalisation in `tests/BetaPlatform.Tests/ProductCodeTests.cs`: trimming, case-insensitive matching, and that an empty or whitespace-only code never matches
- [X] T026 [P] [US2] Unit-test the outcome→status mapping in `tests/BetaPlatform.Tests/ApiOutcomeMappingTests.cs`, **including the `404` branch the sample service cannot reach** — this is what stops the deferred responses being written by accident later (research R11)
- [X] T027 [US2] Run quickstart check 6 (read operations) from `specs/005-jwt-integration-api/quickstart.md`, confirming case/whitespace-insensitive lookup and that no response anywhere contains a product id

**Checkpoint**: The catalogue reads answer in their final shape.

---

## Phase 5: User Story 3 - Add a product (Priority: P2)

**Goal**: An admin submits a product, it is validated and permission-checked for real, and comes
back in the read shape.

**Independent Test**: With an **admin** token, submit a well-formed product and get `201` with
`isActive: true`; submit `{"category":"x"}` and get `400` naming `productCode`, `productName` and
`unit`; submit with a **client** token and get `403`.

> Shares `IProductApiService`, `SampleProductApiService` and `ProductsApiController` with US2, so
> these tasks are **not** parallel with T022–T024.

- [X] T028 [P] [US3] Create `CreateProductRequest` with data annotations mirroring the entity's `MaxLength` exactly — code ≤ 50, names ≤ 200, category ≤ 100, unit ≤ 20; code, name and unit required — in `src/BetaPlatform/ViewModels/Api/CreateProductRequest.cs`. Do **not** accept `isActive` or `createdAt`: a created product is always active (FR-017), and offering a field the platform overrides invites a caller to believe otherwise
- [X] T029 [US3] Add `CreateAsync(CreateProductRequest)` to `src/BetaPlatform/Services/Api/IProductApiService.cs`, with an outcome that can express **conflict** (duplicate code) distinctly from success
- [X] T030 [US3] Implement `CreateAsync` in `src/BetaPlatform/Services/Api/SampleProductApiService.cs`: normalise the code, echo the submission back as an active product, persist nothing. The duplicate-code conflict path is defined but unreachable in this slice
- [X] T031 [US3] Add the `POST /` action with `[Authorize(Roles = DbSeeder.AdminRole)]` to `src/BetaPlatform/Controllers/Api/ProductsApiController.cs`, returning `201` with a `Location: /api/v1/products/{productCode}` header, and mapping the conflict outcome to `409` now (FR-034)
- [X] T032 [P] [US3] Unit-test the create request rules and the conflict→`409` mapping in `tests/BetaPlatform.Tests/ApiValidationTests.cs`: each required field, each max length, and that a valid request passes
- [X] T033 [US3] Run quickstart checks 5 and 6 (create) from `specs/005-jwt-integration-api/quickstart.md`. A `Client` token creating a product must give `403` — a `401` means the role check ran before authentication resolved, a `201` means the role attribute is missing

**Checkpoint**: The catalogue's read and write shapes are both final and enforced.

---

## Phase 6: User Story 4 - Raise a work order by product code (Priority: P3)

**Goal**: A work order is created naming its input and output products **by code**, never by record
number.

**Independent Test**: With an admin or client token, submit a valid work order and get `201` echoing
both codes and `status: "Ready"` as a string; submit `qtyToManufacture: 0` with the same code on
both sides and get `400` naming **only** `qtyToManufacture`.

- [X] T034 [P] [US4] Create `CreateWorkOrderRequest` (`workOrderNumber` ≤ 50 required, `inputProductCode` required, `outputProductCode` required, `plannedStartTime` required, `qtyToManufacture` **> 0**, optional `machineId`/`hourRate`/`lineSetupTimeMinutes`/`workstationCapabilityPerHour` non-negative) in `src/BetaPlatform/ViewModels/Api/CreateWorkOrderRequest.cs`. Zero quantity is rejected, not merely negative
- [X] T035 [P] [US4] Create `WorkOrderResponse` in `src/BetaPlatform/ViewModels/Api/WorkOrderResponse.cs`, echoing both product codes and sending `status` as the **name** (`"Ready"`), never the underlying enum integer
- [X] T036 [US4] Create `IWorkOrderApiService.CreateAsync` in `src/BetaPlatform/Services/Api/IWorkOrderApiService.cs`, with outcomes covering success, duplicate order number, and an unresolvable input **or** output code — the outcome must carry **which** code failed (FR-023)
- [X] T037 [US4] Create `SampleWorkOrderApiService` echoing the submission back with `status = Ready` and persisting nothing, in `src/BetaPlatform/Services/Api/SampleWorkOrderApiService.cs`; register it in `src/BetaPlatform/Program.cs`
- [X] T038 [US4] Create `WorkOrdersApiController` with `[Route("api/v1/work-orders")]`, the bearer scheme, and `[Authorize(Roles = "Admin,Client")]`, exposing `POST /`, in `src/BetaPlatform/Controllers/Api/WorkOrdersApiController.cs`. Map an unresolvable code to **`400`** with the offending field named in `errors` — not `404`, which would tell the caller the endpoint is missing — and a duplicate order number to `409` (contracts/errors.md)
- [X] T039 [P] [US4] Unit-test the work-order rules and outcome mapping in `tests/BetaPlatform.Tests/ApiValidationTests.cs`: `qty <= 0` rejected; each required field named when missing; **the same code as both input and output is accepted** (a rework order legitimately consumes and produces the same product); unresolvable-code and duplicate-number outcomes map to `400` and `409`
- [X] T040 [US4] Run quickstart check 7 from `specs/005-jwt-integration-api/quickstart.md`, confirming both codes are echoed as submitted, `status` is a string, and no product id appears

**Checkpoint**: All four stories are independently functional against the published contract.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T041 [P] Add XML doc comments to every API action in `src/BetaPlatform/Controllers/Api/`, and state in the products and work-orders summaries that their **data is representative in this slice** (FR-033) so the note travels inside the published document rather than in a side file nobody reads
- [X] T042 Compare the generated `/openapi/v1.json` against `specs/005-jwt-integration-api/contracts/openapi.yaml` — paths, schemas, status codes and security scheme must match. The yaml is the agreed target; a difference means the code or the contract is wrong
- [X] T043 [P] Grep `src/BetaPlatform/ViewModels/Api/` and `src/BetaPlatform/Controllers/Api/` for `productId` / `product_id` and confirm **zero** hits (FR-022, SC-004)
- [X] T044 Run quickstart check 8 from `specs/005-jwt-integration-api/quickstart.md` — deactivate a user in the browser and confirm their live token is refused on the **very next** request, not in 8 hours (FR-008). This is the one requirement a stateless token does not give you for free
- [X] T045 Run quickstart check 9 from `specs/005-jwt-integration-api/quickstart.md` — confirm the app refuses to start in Production with a missing key, a key under 32 bytes, and the development placeholder (research R4)
- [X] T046 Run quickstart check 10 from `specs/005-jwt-integration-api/quickstart.md` — click through Dashboard, Machines, Products, Work Orders, Users and `/Dashboard/Display` in the browser and confirm nothing changed; confirm a bearer token does not authenticate a screen and a session cookie does not authenticate `api/v1/*` (SC-008)
- [X] T047 Run quickstart check 11 from `specs/005-jwt-integration-api/quickstart.md` — send a malformed body and confirm a `400 ProblemDetails` with no stack trace, database message, or unhandled `500` (FR-031)
- [X] T048 Run `dotnet test` and confirm the whole suite is green, including the pre-existing tests — this feature touches no existing service, so any pre-existing failure is a regression to investigate before shipping
- [X] T049 Run `dotnet ef migrations list` and confirm **no** migration was added by this feature (constitution Principle IV — 005 changes no schema)

---

## Phase 8: Token renewal (US1, added 2026-08-29)

**Goal**: A caller renews an expired access token with a refresh token instead of re-sending the
password — and cannot use renewal to outlive deactivation.

**Independent Test**: Sign in, exchange the refresh token for a new pair, and confirm the response
carries a new access token and a **different** refresh token; confirm an access token is refused for
renewal and a refresh token is refused as a bearer credential; deactivate the account and confirm the
renewal is refused.

> Extends `JwtOptions.cs`, `JwtTokenService.cs`, `LoginResponse.cs` and `AuthApiController.cs`, so
> these are **not** parallel with each other where they share a file.

- [X] T050 Add `RefreshLifetimeDays` (default 30), `RefreshLifetime`, and the derived `RefreshAudience` (`{Audience}.refresh`) to `src/BetaPlatform/Services/Api/JwtOptions.cs`, and extend `Validate` to refuse a non-positive refresh lifetime **and** one that does not exceed the access lifetime — a refresh token that dies first can never be used, and the symptom would read as an intermittent `401` (research R12)
- [X] T051 [P] Add the `token_use` claim type and its `access` / `refresh` values to `src/BetaPlatform/Services/Api/ApiTokenValidation.cs`
- [X] T052 In `src/BetaPlatform/Services/Api/JwtTokenService.cs`, return an `IssuedTokenPair` from `Issue` and mint the refresh token beside the access token: same signing key, the **refresh audience**, `token_use = refresh`, the security stamp, and **no role claims** (roles are re-read at renewal — FR-039)
- [X] T053 Add `ValidateRefreshTokenAsync` to `src/BetaPlatform/Services/Api/JwtTokenService.cs`: validate signature, issuer, refresh audience and lifetime with `ClockSkew = TimeSpan.Zero`, check `token_use`, and return the subject and its stamp — or `null` for anything else, including a string that is not a token at all (it must not throw out to a `500`)
- [X] T054 [P] Add `refreshToken` and `refreshTokenExpiresAt` to `src/BetaPlatform/ViewModels/Api/LoginResponse.cs`, and create `RefreshRequest` (`refreshToken` required, presence only) in `src/BetaPlatform/ViewModels/Api/RefreshRequest.cs`. Do **not** validate the token's shape — a wrong token is a `401`, not a `400`, for the same reason a wrong password is
- [X] T055 Add `POST refresh` to `src/BetaPlatform/Controllers/Api/AuthApiController.cs`: anonymous, verify the token, re-read the account through `ApiTokenValidation.IsStillValid`, re-read roles, and return a **new pair** in the sign-in response shape. Extract the shared `IssueForAsync` so sign-in and renewal cannot drift (FR-040). Every failure returns one identical `401`
- [X] T056 In `src/BetaPlatform/Services/Api/JwtBearerEventHandlers.cs`, refuse a token whose `token_use` is not `access`, so a refresh token cannot be presented as a bearer credential even if an audience check is ever loosened (FR-041)
- [X] T057 [P] Add `"RefreshLifetimeDays": 30` to `src/BetaPlatform/appsettings.json`, and state renewal in the `OpenApiDocumentTransformer` description so the published document says how to renew
- [X] T058 [P] Extend `tests/BetaPlatform.Tests/JwtTokenServiceTests.cs`: the pair is issued, the refresh token outlives the access token, carries the stamp but **no roles**, the two audiences and `token_use` values differ, a genuine refresh token validates, and an access token / a foreign-key-signed token / an altered token / rubbish are each refused
- [X] T059 Run quickstart checks 3 and 8 — renewal and rotation, the two non-interchangeability refusals, and the one that matters most: a **deactivated** account must not be able to renew (FR-038)

**Checkpoint**: A caller can stay authenticated for 30 days without the account's password in its
configuration, and loses that access one request after being deactivated.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately
- **Foundational (Phase 2)**: needs Setup — **blocks every user story**
- **US1 (Phase 3)**: needs Foundational
- **US2, US3, US4 (Phases 4–6)**: need Foundational to *implement*, and need **US1 complete to
  validate** — every one of their checks requires a token from `POST /auth/login`
- **Polish (Phase 7)**: needs the stories you intend to ship
- **Renewal (Phase 8)**: needs US1 (Phase 3); independent of US2–US4

### User Story Dependencies

| Story | Implement after | Validate after | Notes |
|---|---|---|---|
| US1 (P1) | Phase 2 | Phase 2 | Fully independent — the MVP |
| US2 (P2) | Phase 2 | US1 | Needs a token to test |
| US3 (P2) | US2 | US1 + US2 | Shares three files with US2 (see below) |
| US4 (P3) | Phase 2 | US1 | Independent of US2/US3 — its own service and controller |

**Honest note on independence**: US2–US4 are independently *implementable* but not independently
*testable*, because every one needs a token. US1 is therefore a practical prerequisite for
validating anything else, which is exactly why it is P1.

### File-level serialisation points

- **`Program.cs`** — T010, T011, T012, T013, T014, plus the registrations in T023 and T037. Never
  parallel with each other.
- **`IProductApiService.cs` / `SampleProductApiService.cs` / `ProductsApiController.cs`** — US2
  creates them (T022–T024), US3 extends them (T029–T031). US3 must follow US2 on these files.
- **`ApiValidationTests.cs`** — written by T032 (US3) and extended by T039 (US4). Not parallel.

### Within each user story

DTOs → service interface → sample implementation → controller → tests → manual validation.

---

## Parallel Opportunities

```bash
# Phase 1 — after T001:
T002  Jwt section in appsettings.json
T003  development key in appsettings.Development.json

# Phase 2 — the three standalone files, before anything touches Program.cs:
T005  JwtOptions.cs
T006  ProductCode.cs
T007  ApiTokenValidation.cs

# Phase 3 (US1) — DTOs together, then tests together:
T015  LoginRequest.cs
T016  LoginResponse.cs
# then
T018  JwtTokenServiceTests.cs
T019  ApiTokenValidationTests.cs

# Phase 6 (US4) — DTOs together:
T034  CreateWorkOrderRequest.cs
T035  WorkOrderResponse.cs

# Across stories, once Phase 2 is done: US4 (T034–T038) can be built by a second
# developer alongside US2/US3 — different service, different controller, no shared file.
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 — Setup
2. Phase 2 — Foundational (blocks everything)
3. Phase 3 — US1
4. **Stop and validate**: quickstart checks 1, 2, 4 — especially check 1, the `401`-not-`302` proof
5. At this point you have real, working token authentication. That alone is deployable and is what
   an integrator needs before anything else can be built against the platform.

### Incremental delivery

1. Setup + Foundational → the app starts, publishes a contract, and refuses an unsafe key
2. **+ US1** → sign-in works end to end (MVP)
3. **+ US2** → the catalogue reads in its final shape
4. **+ US3** → the catalogue's write shape and its admin-only rule
5. **+ US4** → work orders by product code — the feature's end goal
6. Polish → the three high-risk checks (T044 revocation, T045 Production startup, T046 no screen
   regression)
7. **+ Renewal (Phase 8)** → a caller stays authenticated without holding the password, and still
   loses access one request after deactivation

### Parallel team strategy

After Phase 2: developer A takes US1 then US2→US3 (they share files); developer B takes US4 in
parallel. Both converge on Phase 7.

---

## Notes

- **Two packages, zero migrations.** If anything in this feature produces a migration, something has
  gone wrong — go back to data-model.md, where every DTO field is mapped to a column that already
  exists.
- **The `404`/`409`/unresolvable-code branches are written in this slice even though the sample
  services cannot reach them.** That is deliberate (FR-034, SC-005): the follow-up behaviour slice
  makes them *reachable*, it does not add them. T026, T032 and T039 test those branches directly.
- **What the follow-up slice changes**: two DI registration lines in `Program.cs`, plus a real
  `ProductApiService` and `WorkOrderApiService` delegating to the existing `IProductService` /
  `IWorkOrderService`. No route, DTO, or status code moves. If wiring the behaviour later requires
  editing a controller, this slice was built wrong.
- Commit after each task or logical group; stop at any checkpoint to validate a story on its own.
