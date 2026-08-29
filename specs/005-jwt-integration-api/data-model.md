# Data Model: Token-Authenticated Integration API

**Feature**: `005-jwt-integration-api` | **Date**: 2026-08-28 | **Phase**: 1

## No schema change

This feature adds **no entity, no property, no index, and no migration**. Every field its contract
exchanges already exists on `Product`, `WorkOrder`, and `ApplicationUser`. Constitution Principle IV
is satisfied by having nothing to satisfy it with.

What this document defines is the **contract shape** — the DTOs the API exchanges — and, for each
field, the existing column it will map to when the behaviour slice wires it up. Fixing that mapping
now is what makes FR-034 checkable: if a DTO field has no column to land in, the contract is wrong
today, not in three months.

---

## Existing entities this feature touches

| Entity | Table | Used for | Written? |
|---|---|---|---|
| `ApplicationUser` | `AspNetUsers` | credential check, `IsActive`, security stamp, roles | ❌ read only |
| `Product` | `products` | target shape of the product DTOs | ❌ not touched this slice |
| `WorkOrder` | `work_orders` | target shape of the work-order DTOs | ❌ not touched this slice |

`ApplicationUser` is read on **every** API request, not only at sign-in — that is the revocation
check (research R3) — and once more at every renewal, where the same predicate decides whether a
refresh token may still buy a new pair (research R12). Those reads are the only database access this
slice performs.

**No refresh-token table.** A refresh token is a second signed JWT, not a stored record, which is how
renewal arrives without a migration (research R12). The trade-off — no per-token revocation list — is
stated in the spec's Assumptions and in `contracts/auth.md`, not hidden here.

---

## Contract shapes

### `LoginRequest`

| Field | Type | Rules |
|---|---|---|
| `email` | string | required; must be a well-formed email address |
| `password` | string | required; non-empty |

Deliberately **not** validated against the password policy (`RequiredLength = 6`). A sign-in request
carrying a 3-character password is a *wrong password*, answered `401`, not a *malformed request*
answered `400` — telling those apart would leak that the policy exists and invite probing. FR-004's
"do not disclose which condition applied" is enforced here as much as in the service.

### `RefreshRequest`

| Field | Type | Rules |
|---|---|---|
| `refreshToken` | string | required; non-empty |

**Presence only.** A token that is present but expired, altered, unknown, or an access token in
disguise is a rejected *credential* (`401`), not a malformed *request* (`400`) — the same line
`LoginRequest` draws between a wrong password and a missing one, and for the same reason: the
`400`/`401` split must not tell someone holding a stolen token which part of it they got wrong.

### `LoginResponse` — returned by **both** sign-in and renewal (FR-040)

| Field | Type | Source |
|---|---|---|
| `accessToken` | string | signed JWT (HS256) |
| `expiresAt` | date-time (UTC, ISO-8601) | issue time + `Jwt:LifetimeHours` (8) |
| `refreshToken` | string | signed JWT (HS256), rotated at every renewal |
| `refreshTokenExpiresAt` | date-time (UTC, ISO-8601) | issue time + `Jwt:RefreshLifetimeDays` (30) |
| `tokenType` | string | constant `"Bearer"` |
| `email` | string | `ApplicationUser.Email` |
| `fullName` | string | `ApplicationUser.FullName` |
| `roles` | string[] | `AspNetUserRoles` → `["Admin"]` / `["Client"]` |

`expiresAt` is absolute UTC rather than a `expiresIn` duration: a caller comparing against its own
clock does not have to remember when it received the response. Note the app stores KSA-local times
elsewhere via `TimeZoneHelper`; token timestamps are **UTC** because JWT `exp`/`nbf` are defined that
way and a bearer token crossing a machine boundary must not carry a local-time assumption.

#### Token claims

| Claim | Access token | Refresh token | Purpose |
|---|---|---|---|
| `sub` | `ApplicationUser.Id` | same | who the caller is |
| `email` | `ApplicationUser.Email` | — | attribution |
| `name` | `ApplicationUser.FullName` | — | attribution |
| `role` (repeated) | each role name | **absent** | FR-007 — permission decisions need no second look-up. Absent from the refresh token because a role baked into a 30-day credential outlives its withdrawal (FR-039) |
| `token_use` | `access` | `refresh` | says which kind this is; checked explicitly at both doors (FR-041) |
| `AspNet.Identity.SecurityStamp` | `ApplicationUser.SecurityStamp` | same | FR-008/FR-038 — compared per request *and* per renewal; a rotated stamp kills both |
| `aud` | `Jwt:Audience` | `Jwt:Audience` + `.refresh` | the mechanism that makes the two non-interchangeable (R12) |
| `jti` | fresh identifier | fresh identifier | distinguishes two tokens issued in the same second |
| `exp`, `nbf`, `iat`, `iss` | standard | standard | validated with `ClockSkew = 0` (R4) |

### `ProductResponse` — one shape for read and create (FR-019)

| Field | Type | Maps to | Notes |
|---|---|---|---|
| `productCode` | string | `products.product_code` | the external identity — max 50 |
| `productName` | string | `products.product_name` | primary/Arabic name — max 200 |
| `productNameEnglish` | string \| null | `products.product_name_english` | max 200 |
| `category` | string \| null | `products.category` | max 100 |
| `unit` | string | `products.unit` | max 20, e.g. `kg` |
| `isActive` | boolean | `products.is_active` | |

**`product_id` is deliberately absent** — FR-022. It is the one field a reader might expect and must
not find; its absence is the feature's central promise, not an omission.

### `CreateProductRequest`

| Field | Required | Rules | Maps to |
|---|---|---|---|
| `productCode` | ✅ | non-blank after trim; ≤ 50 | `product_code` |
| `productName` | ✅ | non-blank; ≤ 200 | `product_name` |
| `productNameEnglish` | ❌ | ≤ 200 | `product_name_english` |
| `category` | ❌ | ≤ 100 | `category` |
| `unit` | ✅ | non-blank; ≤ 20 | `unit` |

`isActive` is not accepted: a created product is active (FR-017), and offering a field the platform
overrides invites a caller to believe otherwise. `createdAt` is likewise server-assigned.

Lengths mirror the `MaxLength` attributes on the entity exactly. They are asserted at the DTO so a
caller gets a `400` naming the field, rather than a database truncation error later.

### `CreateWorkOrderRequest`

| Field | Required | Rules | Maps to |
|---|---|---|---|
| `workOrderNumber` | ✅ | non-blank; ≤ 50 | `work_orders.work_order_number` |
| `inputProductCode` | ✅ | non-blank | resolved → `input_product_id` |
| `outputProductCode` | ✅ | non-blank | resolved → `output_product_id` |
| `plannedStartTime` | ✅ | valid date-time | `planned_start_time` |
| `qtyToManufacture` | ✅ | **> 0** | `qty_to_manufacture` |
| `machineId` | ❌ | positive when present | `machine_id` |
| `hourRate` | ❌ | ≥ 0 when present | `hour_rate` |
| `lineSetupTimeMinutes` | ❌ | ≥ 0 when present | `line_setup_time_minutes` |
| `workstationCapabilityPerHour` | ❌ | ≥ 0 when present | `workstation_capability_per_hour` |

The two product codes are the point of the feature: the caller sends codes, the platform resolves
them to `input_product_id` / `output_product_id`, and those ids never appear in the contract in
either direction (FR-022).

`machineId` is the one identifier that **is** an internal number, and it stays one: FR-022 constrains
*products*, machines have no equivalent external code on this surface, and inventing one would be
scope this feature was not asked for. Called out here so the asymmetry reads as a decision.

Quantity is rejected at zero, not merely at negative — an order to manufacture nothing is a mistake
worth catching at the edge (US4 §3).

### `WorkOrderResponse`

| Field | Type | Maps to |
|---|---|---|
| `workOrderNumber` | string | `work_order_number` |
| `inputProductCode` | string | resolved from `input_product_id` — **echoed as submitted** (FR-027) |
| `outputProductCode` | string | resolved from `output_product_id` |
| `status` | string | `status` — always `"Ready"` on create (FR-026) |
| `plannedStartTime` | date-time | `planned_start_time` |
| `qtyToManufacture` | decimal | `qty_to_manufacture` |
| `machineId` | int \| null | `machine_id` |
| `hourRate` | decimal \| null | `hour_rate` |
| `lineSetupTimeMinutes` | int \| null | `line_setup_time_minutes` |
| `workstationCapabilityPerHour` | decimal \| null | `workstation_capability_per_hour` |

`status` travels as the **name** (`"Ready"`), not the underlying `WorkOrderStatus` integer `1`. A
caller should not have to learn an enum's numbering, and the numbering is an internal detail free to
change; the name is the contract.

---

## Validation rules, and where each is enforced

| Rule | Enforced by | Live this slice? |
|---|---|---|
| Required fields, max lengths, `qty > 0`, email format | data annotations on the DTO → `[ApiController]` → `400 ValidationProblemDetails` | ✅ yes |
| Unparsable JSON body | model binding → `400` in the same shape | ✅ yes |
| Unknown extra fields ignored, not refused | default `System.Text.Json` behaviour — left as-is | ✅ yes |
| Caller authenticated | JWT bearer scheme → `401` | ✅ yes |
| Refresh token genuine — signature, issuer, refresh audience, not expired | `IJwtTokenService.ValidateRefreshTokenAsync` → `401` | ✅ yes |
| Refresh token still usable — account present, active, stamp unchanged | `ApiTokenValidation.IsStillValid` at renewal → `401` | ✅ yes |
| Caller holds the required role | `[Authorize(Roles = …)]` → `403` | ✅ yes |
| Account still active / stamp unchanged | `OnTokenValidated` → `401` | ✅ yes |
| Product code exists | `IProductApiService` | ❌ deferred — response specified, not yet produced |
| Product code unique | `IProductApiService` | ❌ deferred |
| Work-order number unique | `IWorkOrderApiService` | ❌ deferred |
| Input/output codes resolvable | `IWorkOrderApiService` | ❌ deferred |

The split is the slice boundary in one table: everything that is a property of the **request** is
enforced now; everything that is a property of the **data** is specified now and enforced later.

---

## Product code normalisation (R9)

One helper, used by every code comparison and by the eventual real implementations:

```
normalise(code) = code.Trim()          // stored and echoed in this form
match(a, b)     = normalise(a).Equals(normalise(b), OrdinalIgnoreCase)
```

Centralised deliberately: MySQL's default collation is already case-insensitive, so a real
implementation written without thinking would match case-insensitively *by accident* on the server
while any in-memory check matched case-sensitively. One helper makes the two halves agree on purpose.

---

## Authorization map

| Operation | Admin | Client | Other authenticated | Anonymous |
|---|---|---|---|---|
| `POST /api/v1/auth/login` | ✅ | ✅ | ✅ | ✅ |
| `POST /api/v1/auth/refresh` | ✅ | ✅ | ✅ | ✅ (the refresh token is the credential) |
| `GET /api/v1/products` | ✅ | ✅ | ✅ | ❌ 401 |
| `GET /api/v1/products/{code}` | ✅ | ✅ | ✅ | ❌ 401 |
| `POST /api/v1/products` | ✅ | ❌ 403 | ❌ 403 | ❌ 401 |
| `POST /api/v1/work-orders` | ✅ | ✅ | ❌ 403 | ❌ 401 |

Mirrors the browser screens exactly (004's `contracts/user-management.md`): products are
administrative to write and open to read; work orders are open to Admin and Client. One permission
model, not two that drift.
