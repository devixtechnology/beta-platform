# Implementation Plan: Token-Authenticated Integration API (Products & Work Orders)

**Branch**: `main` (no feature branch — no `before_specify` git hook is registered) | **Date**: 2026-08-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-jwt-integration-api/spec.md`

> **Amended 2026-08-29** — token renewal (`POST /api/v1/auth/refresh`) added at the author's
> request, superseding the original FR-003. Six operations, still no schema change: the refresh
> token is a second signed JWT with its own audience, not a stored record (research R12). Nothing
> else in this plan moves.

> **Amended 2026-09-01** — a work order names **several** input product codes and one output code, at
> the author's request (FR-042 … FR-044, research R13). Still six operations and still no migration
> *here*, because this slice persists nothing; the follow-up behaviour slice gains one — a
> `work_order_input_products` join table — and that is stated wherever this plan promises the
> follow-up is a registration change.

## Summary

Open a small, machine-consumable surface on the existing MVC monolith: sign in for a bearer token,
renew it without re-sending the password, read and add products, and raise a work order naming its
input and output products **by product code** — several inputs, one output. Six operations, no schema
change.

The slice is contract-first by the author's decision. **Authentication, renewal, permission
enforcement, and request validation are genuinely implemented**; the five business operations answer with
representative data and touch no product or work-order table. What makes that a plan rather than a
placeholder is the seam: controllers depend on `IProductApiService` / `IWorkOrderApiService`, which
this slice satisfies with sample implementations and the follow-up slice satisfies with real ones
that delegate to the existing `IProductService` / `IWorkOrderService` — two lines in `Program.cs`,
and no route, DTO, or status code moves (FR-034, SC-005). Since the 2026-09-01 amendment that slice
also carries one migration, for the input join table (R13); the seam itself is unchanged.

The technical crux is coexistence. `AddIdentity` has already claimed the default authentication
scheme for cookies, so the JWT handler is registered as an **additional, non-default** scheme and
named explicitly on each API controller. That one decision is what keeps an unauthenticated API call
returning a bare `401` instead of a `302` to `/Auth/Login`, and what guarantees the browser screens
behave exactly as they do today.

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`)

**Primary Dependencies**: ASP.NET Core MVC 9; ASP.NET Core Identity 9 (`UserManager`,
`SignInManager`, security stamps); EF Core 9 + `Pomelo.EntityFrameworkCore.MySql` 9.0.0 (existing,
untouched by this feature). **New**: `Microsoft.AspNetCore.Authentication.JwtBearer` 9.0.0 and
`Microsoft.AspNetCore.OpenApi` 9.0.0 — both first-party, matched to the pinned framework version —
plus `Scalar.AspNetCore` 2.17.2, which renders the generated document as an interactive reference at
`/docs` (research R6, revised).

**Storage**: MySQL 8.0 via EF Core. **No migration.** This feature reads the Identity tables
(`AspNetUsers`, `AspNetUserRoles`) for authentication, renewal, and revocation only; it neither reads
nor writes `products` or `work_orders` in this slice. Refresh tokens are **not** stored — they are
signed JWTs with their own audience and their own expiry (research R12), which is what keeps the
no-migration promise while still offering renewal.

**Testing**: xUnit in the existing `tests/BetaPlatform.Tests`, with
`Microsoft.EntityFrameworkCore.InMemory` for anything needing a context. No new test dependency; no
in-process HTTP host this slice (research R11).

**Target Platform**: Linux/Windows server, single deployable ASP.NET Core process.

**Project Type**: Web application — a single MVC monolith gaining an attribute-routed API area.

**Performance Goals**: SC-007 — a response within 2 seconds under normal plant load. Not a
constraint in practice: the sample services do no I/O, and the only per-request database work is one
primary-key lookup of the caller for revocation (research R3).

**Constraints**:
- Adding this surface must change **no** existing screen behaviour (SC-008) — hence the non-default
  scheme (R1) and attribute routing that cannot collide with the conventional route (R8).
- Deactivation must revoke a live token immediately, not at its 8-hour expiry (FR-008 / R3), and
  must stop its renewals with the same predicate (FR-038 / R12).
- Representative responses must be shape-identical to the eventual real ones (FR-034 / R7).
- No default JWT signing key may be usable in Production (R4), mirroring 004's no-default-admin-
  password rule.

**Scale/Scope**: A handful of integration callers; 6 endpoints; ~3 controllers, ~9 DTOs, 3 services,
3 new packages, 0 migrations.

## Constitution Check

*GATE: evaluated before Phase 0, re-evaluated after Phase 1 design.*

| Principle | Verdict | Basis |
|---|---|---|
| **I. Monolithic Architecture** | ✅ Pass | New controllers, DTOs, and services live inside the existing `src/BetaPlatform` project. No new process, no network hop, no separate API host — the API is a folder, not a service (R1 alternatives). |
| **II. SOLID Design Principles** | ✅ Pass | Each new type has one reason to change: `JwtTokenService` issues tokens, the API services answer API operations, controllers map outcomes to status codes. All dependencies are interfaces resolved through the built-in DI container, which is precisely what makes the Phase-2 swap a registration change (R7). Interfaces are small and per-area rather than one broad `IApiService`. |
| **III. Simplicity First (YAGNI)** | ⚠️ Pass with one logged item | Deliberately declined: a policy scheme selector (R1), a token denylist (R3), a custom error envelope (R5), Swashbuckle (R6), and a versioning library (R8) — each rejected as machinery for a problem this feature does not have. The one addition that *looks* speculative — two service interfaces whose only implementations return canned data — is logged in Complexity Tracking below, because the seam is the requirement rather than a guess. |
| **IV. EF Core Migrations for All Schema (NON-NEGOTIABLE)** | ✅ Pass | No entity added, no property added, **no migration** — nothing in this feature writes to the database at all, and no raw SQL is used. Identity access goes through `UserManager`. Since the 2026-09-01 amendment one contract field (the input code **list**) has no column behind it yet; the principle is untouched because the slice stores nothing, and the join table it will need arrives in the follow-up slice **as an EF Core migration**, which is exactly what this principle demands (R13). |
| **V. MVC Separation of Concerns** | ✅ Pass | Controllers coordinate and map; they hold no business rule and no `DbContext`. Token issuing and the operation outcomes sit in services. There are no views on this surface. The sample data lives in the service implementations, not in the controllers — the specific mistake that would break SC-005. |

**Gate result: PASS.** One item carried to Complexity Tracking; no unjustified violation.

**Post-Phase-1 re-evaluation**: unchanged. The design artifacts added DTOs and contracts but no new
abstraction, no new dependency beyond the two named packages, and no schema change. The Phase 1
`data-model.md` maps every DTO field to a column that already exists, with the single exception the
2026-09-01 amendment introduces — the input code list, which this slice does not store and the
follow-up slice migrates for (R13). Principle IV stays untouched by construction.

## Project Structure

### Documentation (this feature)

```text
specs/005-jwt-integration-api/
├── plan.md              # This file
├── spec.md              # Feature specification (input)
├── research.md          # Phase 0 output — R1..R12 decisions
├── data-model.md        # Phase 1 output — DTO shapes, validation, mapping to existing entities
├── quickstart.md        # Phase 1 output — runnable validation guide
├── contracts/           # Phase 1 output
│   ├── openapi.yaml     #   machine-readable contract (FR-032)
│   ├── auth.md          #   sign-in, renewal, token, revocation
│   ├── products.md      #   list / get one / create
│   ├── work-orders.md   #   create by product code
│   ├── errors.md        #   the single error shape and status-code map
│   └── postman_collection.json  #   one runnable request per operation, happy path only
├── checklists/
│   └── requirements.md  # spec quality checklist (all pass)
└── tasks.md             # Phase 2 output — created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

Only the marked entries are new; everything else is existing structure shown for orientation.

```text
src/BetaPlatform/
├── Program.cs                          # MODIFIED — JWT scheme, OpenAPI, ProblemDetails, DI, MapOpenApi
├── BetaPlatform.csproj                 # MODIFIED — + JwtBearer 9.0.0, + OpenApi 9.0.0, XML docs on
├── appsettings.json                    # MODIFIED — "Jwt" section, empty SigningKey
├── appsettings.Development.json        # MODIFIED — development signing key
├── Controllers/
│   ├── Api/                            # NEW — attribute-routed, api/v1/*
│   │   ├── AuthApiController.cs        #   NEW — POST api/v1/auth/login, POST api/v1/auth/refresh
│   │   ├── ProductsApiController.cs    #   NEW — GET/POST api/v1/products
│   │   └── WorkOrdersApiController.cs  #   NEW — POST api/v1/work-orders
│   └── …                               # existing MVC controllers — untouched
├── ViewModels/
│   └── Api/                            # NEW — request/response DTOs (see data-model.md)
│       ├── LoginRequest.cs   RefreshRequest.cs   LoginResponse.cs
│       ├── ProductResponse.cs   CreateProductRequest.cs
│       └── CreateWorkOrderRequest.cs   WorkOrderResponse.cs
├── Services/
│   ├── Api/                            # NEW
│   │   ├── IJwtTokenService.cs / JwtTokenService.cs      # NEW — issues token pairs, verifies refresh tokens
│   │   ├── IProductApiService.cs / SampleProductApiService.cs      # NEW — swapped in Phase 2
│   │   ├── IWorkOrderApiService.cs / SampleWorkOrderApiService.cs  # NEW — swapped in Phase 2
│   │   ├── JwtOptions.cs               # NEW — bound to the "Jwt" section
│   ├── OpenApiDocumentTransformer.cs # NEW — slice note + bearer scheme in the document
│   │   └── ProductCode.cs              # NEW — the one normalisation helper (R9)
│   ├── ServiceResult.cs                # REUSED — outcome type for the API services
│   └── …                               # existing services — untouched
└── Data/                               # UNTOUCHED — no entity change, no migration

tests/BetaPlatform.Tests/
├── JwtTokenServiceTests.cs             # NEW — claims, expiry, stamp
├── ProductCodeTests.cs                 # NEW — trim + case-insensitive (R9)
├── ApiValidationTests.cs               # NEW — request-shape rules
├── ApiOutcomeMappingTests.cs           # NEW — outcome → status code, incl. deferred branches
└── …                                   # existing tests — untouched
```

**Structure Decision**: Single project, matching the constitution's monolith principle and the
layout features 001–004 already established. The API is separated by **folder and route prefix**
(`Controllers/Api`, `Services/Api`, `ViewModels/Api`, `api/v1/*`), not by project or process. This
keeps one deployable and one DI container while making the new surface obvious to a reader and
trivially removable if the integration is ever retired. Tests go into the existing xUnit project
rather than a new one, since nothing here needs a different runner or host.

## Phase 2 handover — what the follow-up slice changes

Recorded here so the boundary is unambiguous when someone picks it up:

| Change | File |
|---|---|
| Register real implementations instead of sample ones | `Program.cs` — 2 registration lines |
| Real product read/create delegating to `IProductService` | new `ProductApiService.cs` |
| Real code→product resolution and work-order create delegating to `IWorkOrderService` | new `WorkOrderApiService.cs` |
| Nothing else | routes, DTOs, status codes, contracts, and clients stay exactly as published |

The `404` / `409` / unresolvable-code branches already exist in the controllers this slice ships;
Phase 2 makes them **reachable**, it does not add them.

## Complexity Tracking

> Filled because the Constitution Check flagged one item under Principle III.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `IProductApiService` / `IWorkOrderApiService` — two interfaces whose only implementations in this slice return canned data | The author's decision is a contract-only slice whose contract must survive the later behaviour wiring untouched (FR-034, SC-005). The seam **is** the requirement, not a guess about a future need: without it, "wire the behaviour later" means editing controllers, and controllers are where routes, status codes, and response shapes live. Reuses the existing `ServiceResult<T>` rather than inventing a result type, holding the addition to two interfaces and two classes. | *Canned DTOs returned inline from controllers* — fewest types today, but it puts sample data and, later, every deferred behaviour into the controller, breaking Principle V and SC-005 in the same move. *Depending directly on the existing `IProductService` / `IWorkOrderService`* — those return entities and hit the database, which is exactly the behaviour this slice defers; it would also push entity→DTO mapping and code→id resolution into controllers. |
