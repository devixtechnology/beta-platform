<!-- SPECKIT START -->
Active feature: **005-jwt-integration-api** — Token-Authenticated Integration API (products &
work orders), a **contract-first** slice: authentication, renewal, permissions and request
validation are genuinely implemented; the five business operations return representative data and
persist nothing.

Read the current plan and its design artifacts for full context:
- Plan: `specs/005-jwt-integration-api/plan.md`
- Spec (incl. the slice-boundary table): `specs/005-jwt-integration-api/spec.md`
- Research & decisions R1-R13: `specs/005-jwt-integration-api/research.md`
- Data model (no schema change): `specs/005-jwt-integration-api/data-model.md`
- Contracts: `specs/005-jwt-integration-api/contracts/` (openapi.yaml, auth, products,
  work-orders, errors, postman_collection.json — one request per operation, happy path only)
- Validation guide: `specs/005-jwt-integration-api/quickstart.md`
- Constitution (non-negotiable): `.specify/memory/constitution.md`

005 scope: **no EF migration** — nothing is added to the schema and nothing is written to `products`
or `work_orders`. Six endpoints under `api/v1/*`: `POST /auth/login`, `POST /auth/refresh`,
`GET /products`, `GET /products/{productCode}`, `POST /products` (Admin only),
`POST /work-orders` (Admin or Client).
Products are addressed by **product code** everywhere — internal `product_id` never appears in the
contract, in either direction, and a work order names its input and output products by code: since
the 2026-09-01 amendment `inputProductCodes` is a **list** (≥ 1 entry, no blank, no repeats) and
`outputProductCode` stays single (FR-042 … FR-044, research R13). That list is a contract shape only
— `work_orders` stores one input product, so the follow-up behaviour slice carries a
`work_order_input_products` join table; **005 itself still adds no migration**.
Key constraints: (a) `AddIdentity` already owns the default auth scheme, so JWT bearer is registered
as an **additional, non-default** scheme and named explicitly on each API controller — this is what
makes an unauthenticated API call return a bare `401` instead of redirecting to `/Auth/Login`;
(b) access tokens last **8 h**, renewed either by signing in again or at `POST /auth/refresh` with
the **refresh token** issued beside them (30 days, `Jwt:RefreshLifetimeDays`, rotated on every use)
— that refresh token is a second signed JWT with the audience `{Jwt:Audience}.refresh` and a
`token_use` claim, **not** a stored record, so renewal still needs no migration; it carries no role
claims because roles are re-read at renewal, and renewal re-runs the same `IsStillValid` check, so a
deactivated account cannot renew (research R12, FR-036 … FR-041);
(c) deactivation must revoke a live token on the **next request** via a security-stamp claim checked
in `OnTokenValidated`, mirroring the existing cookie `SecurityStampValidator`; (d) one error shape —
RFC 9457 `ProblemDetails` / `ValidationProblemDetails`; (e) the app refuses to start without a valid
`Jwt:SigningKey`, and refuses the dev placeholder in Production, mirroring 004's no-default-admin-
password rule; (f) controllers depend on `IProductApiService` / `IWorkOrderApiService`, satisfied
now by `Sample*` implementations — the follow-up behaviour slice swaps two DI registrations and
changes **no** route, DTO, or status code. New packages: `Microsoft.AspNetCore.Authentication.JwtBearer`
9.0.0, `Microsoft.AspNetCore.OpenApi` 9.0.0 (contract at `/openapi/v1.json`) and `Scalar.AspNetCore`
2.17.2 (interactive reference at `/docs`, anonymous, renders that same document). Existing MVC
screens, services, and permissions are untouched.

Prior features (foundation, still current): **001-phase-1-core** (`specs/001-phase-1-core/`),
**002-ui-modernization** (`specs/002-ui-modernization/` — design system, theme toggle, creation
pages), **004-phase1-feedback** (`specs/004-phase1-feedback/` — machine running state, user
administration, machine-details polling, Input Weight, `/Dashboard/Display`, duration-weighted
uptime, EdgeView rename). Change request **003** shipped without a spec folder: `oee_data`
total_weight/total_count split, single live work-order output, work-order inputs, Edge View rebrand,
work-order hour rate / setup time / capability fields (migrations `20260709193126`, `20260709195313`).

Stack: ASP.NET Core .NET 9 MVC monolith, EF Core 9 + Pomelo MySQL 8.0, ASP.NET Core Identity,
bilingual AR/EN + RTL via IStringLocalizer/.resx. All schema via EF Core migrations only (no raw SQL
DDL). `oee_data` and `power_data` are compatibility-locked, read-only telemetry tables written by the
IoT team — never add an ingestion API. Dashboard refreshes by ~5 s client polling (no SignalR).
Reference project for schema & front-end reuse: `E:\Projects\Work\S\web`.
<!-- SPECKIT END -->
