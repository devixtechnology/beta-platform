# Quickstart & Validation: Token-Authenticated Integration API

**Feature**: `005-jwt-integration-api` | **Phase**: 1

How to run this feature and prove it works. Shapes and status codes live in
[`contracts/`](./contracts/); field-by-field rules live in [`data-model.md`](./data-model.md) — this
guide does not repeat them.

## Prerequisites

- .NET 9 SDK, MySQL 8.0 reachable via `ConnectionStrings:DefaultConnection`.
- An admin account (seeded by `DbSeeder`) and — for the permission checks — a `Client` account
  created through `/Users` in the browser.
- No migration to apply. If `dotnet ef migrations list` shows something pending for this feature,
  something has gone wrong: this feature changes no schema.

## Configuration

`appsettings.Development.json` gains a development signing key; `appsettings.json` gains the same
section with an **empty** `SigningKey` so the shape is discoverable without shipping a secret.

```jsonc
"Jwt": {
  "Issuer": "BetaPlatform",
  "Audience": "BetaPlatformApi",
  "SigningKey": "<development-only, at least 32 bytes>",
  "LifetimeHours": 8,
  "RefreshLifetimeDays": 30
}
```

Refresh tokens are signed with the same key and carry the audience `BetaPlatformApi.refresh`, which
is what stops either token being used in the other's place. `RefreshLifetimeDays` must exceed
`LifetimeHours`, and startup refuses a configuration where it does not.

In Production supply `Jwt__SigningKey` from the environment or a secret store. The app **refuses to
start** without it — see check 9.

## Run

```powershell
dotnet build
dotnet run --project src/BetaPlatform
```

Then confirm the published contract is being served (FR-032):

```powershell
curl.exe http://localhost:5000/openapi/v1.json
```

It should list all six operations and match [`contracts/openapi.yaml`](./contracts/openapi.yaml).

Then open the interactive reference in a browser — no sign-in required:

```
http://localhost:5000/docs
```

It renders that same document, so the two cannot disagree. Use its authentication box to paste a
token from check 2 and call the endpoints from the page. Its JavaScript is served by the application
itself, so it works without internet access.

> Use `curl.exe`, not `curl`, in PowerShell — the bare name is an alias for `Invoke-WebRequest`,
> which does not accept these flags.

## Postman

[`contracts/postman_collection.json`](./contracts/postman_collection.json) is the same surface as a
runnable collection: **one request per operation, happy path only**. Import it, set `baseUrl` and the
admin credentials, run **Sign in**, and everything else picks the token up automatically; run **Renew
tokens** when the access token expires.

The failure cases are deliberately *not* in the collection — they live in the checks below, where a
person runs them on purpose rather than tripping over them in a collection run.

---

## Validation checks

Checks 1–4 and 9 are the ones worth running first: they cover what is genuinely implemented and the
mistakes most likely to be made.

### 1. An API call without a token returns 401, not a login redirect ⭐

The single most likely thing to be got wrong in this feature (research R1), and the one thing unit
tests cannot prove.

```powershell
curl.exe -i http://localhost:5000/api/v1/products
```

**Expect** `401` with `application/problem+json`.
**Fail** if you see `302` and a `Location: /Auth/Login` header — the cookie handler answered, which
means the API controller is not naming the bearer scheme explicitly. FR-009, SC-008.

### 2. Sign in and use the token

```powershell
$r = curl.exe -s -X POST http://localhost:5000/api/v1/auth/login `
  -H "Content-Type: application/json" `
  -d '{\"email\":\"admin@beta.local\",\"password\":\"Admin@123\"}' | ConvertFrom-Json
$r.expiresAt          # ~8 hours ahead, UTC
$r.roles              # ["Admin"]
$token = $r.accessToken
curl.exe -s http://localhost:5000/api/v1/products -H "Authorization: Bearer $token"
```

**Expect** a token, `expiresAt` about 8 hours out, and a product array. FR-001, FR-002.

Paste the token into <https://jwt.io> and confirm the claims: `sub`, `email`, `name`, one `role` per
role, and `AspNet.Identity.SecurityStamp`. Confirm no secret is in there — the token is signed, not
encrypted, so anything in it is readable by whoever holds it.

### 3. Renewal, and what it re-reads

```powershell
$r = curl.exe -s -X POST http://localhost:5000/api/v1/auth/login `
  -H "Content-Type: application/json" `
  -d '{\"email\":\"admin@beta.local\",\"password\":\"Admin@123\"}' | ConvertFrom-Json

$body = '{\"refreshToken\":\"' + $r.refreshToken + '\"}'
$n = curl.exe -s -X POST http://localhost:5000/api/v1/auth/refresh `
  -H "Content-Type: application/json" -d $body | ConvertFrom-Json

$n.accessToken -ne $r.accessToken     # True  — a new access token
$n.refreshToken -ne $r.refreshToken   # True  — rotated (FR-037)
$n.expiresAt                          # ~8 hours ahead again
```

**Expect** a `200` in the **same shape** sign-in returns, a new access token, and a **different**
refresh token. FR-036, FR-037, FR-040.

```powershell
# the two kinds are not interchangeable (FR-041)
curl.exe -i -X POST http://localhost:5000/api/v1/auth/refresh -H "Content-Type: application/json" `
  -d ('{\"refreshToken\":\"' + $r.accessToken + '\"}')
curl.exe -i http://localhost:5000/api/v1/products -H ("Authorization: Bearer " + $r.refreshToken)
```

**Expect** `401` from both — an access token cannot renew, and a refresh token cannot open anything.
A `200` from either means the audience separation is gone.

```powershell
# a missing field is a 400; a wrong token is a 401
curl.exe -i -X POST http://localhost:5000/api/v1/auth/refresh -H "Content-Type: application/json" -d '{}'
curl.exe -i -X POST http://localhost:5000/api/v1/auth/refresh -H "Content-Type: application/json" -d '{\"refreshToken\":\"nonsense\"}'
```

**Expect** `400` naming `refreshToken`, then `401` — never a `500`. FR-040.

Renewal against a **deactivated** account is check 8.

### 4. Sign-in failures are indistinguishable

```powershell
# wrong password, unknown email, and a deactivated account
curl.exe -i -X POST http://localhost:5000/api/v1/auth/login -H "Content-Type: application/json" -d '{\"email\":\"admin@beta.local\",\"password\":\"wrong\"}'
curl.exe -i -X POST http://localhost:5000/api/v1/auth/login -H "Content-Type: application/json" -d '{\"email\":\"nobody@beta.local\",\"password\":\"Admin@123\"}'
```

**Expect** all three to return an identical `401` `"Invalid credentials."` — no hint about which
condition applied. FR-004.

```powershell
# a broken request is 400, not 401
curl.exe -i -X POST http://localhost:5000/api/v1/auth/login -H "Content-Type: application/json" -d '{\"email\":\"not-an-email\"}'
```

**Expect** `400` naming `email` and `password`.

### 5. Roles are enforced — 403, not 401

Sign in as the **Client** account, then:

```powershell
curl.exe -i -X POST http://localhost:5000/api/v1/products -H "Authorization: Bearer $clientToken" `
  -H "Content-Type: application/json" -d '{\"productCode\":\"X-1\",\"productName\":\"x\",\"unit\":\"kg\"}'
```

**Expect** `403`. A `401` here means the role check ran before authentication resolved; a `201` means
the `[Authorize(Roles = "Admin")]` is missing. FR-006, FR-018.

Then confirm the same Client token **can** create a work order (check 7) — that asymmetry is the
authorization map in [`data-model.md`](./data-model.md).

### 6. Products read and create

```powershell
curl.exe -s "http://localhost:5000/api/v1/products?activeOnly=true" -H "Authorization: Bearer $token"
curl.exe -s http://localhost:5000/api/v1/products/RM-STEEL-01 -H "Authorization: Bearer $token"
curl.exe -s http://localhost:5000/api/v1/products/rm-steel-01 -H "Authorization: Bearer $token"
curl.exe -s "http://localhost:5000/api/v1/products/%20RM-Steel-01%20" -H "Authorization: Bearer $token"
```

**Expect** the last three to return the **same** product — codes are trimmed and matched
case-insensitively (R9).

**Expect** no `productId` field anywhere in any response. Its absence is FR-022 and the promise of
the whole feature.

```powershell
curl.exe -i -X POST http://localhost:5000/api/v1/products -H "Authorization: Bearer $token" `
  -H "Content-Type: application/json" `
  -d '{\"productCode\":\"RM-NEW-01\",\"productName\":\"جديد\",\"productNameEnglish\":\"New\",\"unit\":\"kg\"}'
```

**Expect** `201`, a `Location` header, `isActive: true`, and the same shape the reads return.

```powershell
# validation: missing required fields and an over-length code
curl.exe -i -X POST http://localhost:5000/api/v1/products -H "Authorization: Bearer $token" `
  -H "Content-Type: application/json" -d '{\"category\":\"x\"}'
```

**Expect** `400` with an `errors` object naming `productCode`, `productName`, and `unit`.

### 7. Work order by product code

```powershell
curl.exe -i -X POST http://localhost:5000/api/v1/work-orders -H "Authorization: Bearer $token" `
  -H "Content-Type: application/json" `
  -d '{\"workOrderNumber\":\"WO-TEST-1\",\"inputProductCode\":\"RM-STEEL-01\",\"outputProductCode\":\"FG-PANEL-07\",\"plannedStartTime\":\"2026-08-29T06:00:00\",\"qtyToManufacture\":100}'
```

**Expect** `201`; both codes echoed exactly as submitted; `status: "Ready"` as a **string**, not `1`;
no product id fields. FR-026, FR-027.

```powershell
# quantity must be > 0, and the same code may be both input and output
curl.exe -i -X POST http://localhost:5000/api/v1/work-orders -H "Authorization: Bearer $token" `
  -H "Content-Type: application/json" `
  -d '{\"workOrderNumber\":\"WO-TEST-2\",\"inputProductCode\":\"RM-STEEL-01\",\"outputProductCode\":\"RM-STEEL-01\",\"plannedStartTime\":\"2026-08-29T06:00:00\",\"qtyToManufacture\":0}'
```

**Expect** `400` naming `qtyToManufacture` **only** — the repeated code is a legitimate rework order
and must not be rejected.

### 8. Deactivation revokes a live token — and stops its renewals ⭐

The one requirement a stateless token does not give you for free (FR-008, R3).

1. Sign in as the Client account and keep the token.
2. In the browser, as an admin, deactivate that user at `/Users`.
3. Reuse the token immediately:

```powershell
curl.exe -i http://localhost:5000/api/v1/products -H "Authorization: Bearer $clientToken"
```

**Expect** `401` on the very next request — not in 8 hours. A `200` means the security-stamp check in
`OnTokenValidated` is missing or is not comparing against the store.

4. Now try to renew with the **refresh token** from that same sign-in:

```powershell
curl.exe -i -X POST http://localhost:5000/api/v1/auth/refresh -H "Content-Type: application/json" `
  -d ('{\"refreshToken\":\"' + $clientRefreshToken + '\"}')
```

**Expect** `401`. A `200` here would be the worst bug in the feature: a deactivated account renewing
its way back in for another 8 hours, every 8 hours, for 30 days. FR-038.

Changing a password rotates the same stamp, so it invalidates outstanding access **and** refresh
tokens. That is correct behaviour, not a bug — and it is the lever to pull if a refresh token is
believed to have leaked.

### 9. Production refuses to start without a signing key ⭐

```powershell
$env:ASPNETCORE_ENVIRONMENT="Production"; $env:Jwt__SigningKey=""
dotnet run --project src/BetaPlatform
```

**Expect** startup to fail immediately with a clear message about `Jwt:SigningKey`. Also confirm a
short key (< 32 bytes) is refused, and that the development placeholder is refused in Production —
mirroring 004's no-default-admin-password rule (R4). Reset the variables afterwards.

### 10. Existing screens are unaffected

Sign in through the browser at `/Auth/Login` and click through Dashboard, Machines, Products, Work
Orders, Users, and `/Dashboard/Display`. **Expect** no change whatsoever — same pages, same
permissions, same behaviour. SC-008.

Also confirm the two doors do not overlap: a bearer token does **not** authenticate a browser page,
and a session cookie does **not** authenticate `api/v1/*`.

### 11. No diagnostics leak

Send a deliberately malformed body:

```powershell
curl.exe -i -X POST http://localhost:5000/api/v1/products -H "Authorization: Bearer $token" `
  -H "Content-Type: application/json" -d '{ this is not json'
```

**Expect** `400` in the standard `ProblemDetails` shape — never a stack trace, a database message, or
an unhandled `500`. FR-031.

---

## Unit tests

```powershell
dotnet test
```

Covers what is genuinely decided in this slice: token claims and expiry, refresh-token issuing,
rotation and verification (including the two refusals that keep the kinds apart), the security-stamp
revocation predicate, product-code normalisation, request-validation rules, and the outcome →
status-code mapping **including the `404`/`409` branches the sample services cannot yet reach**
(research R11). Those branch tests are what keep the deferred responses from being written by
accident when the behaviour slice arrives.

The existing suite must stay green — this feature touches no existing service.

## What this slice does *not* prove

Products and work orders are **not** persisted, and codes are **not** resolved against the real
catalogue. Creating the same product code twice returns `201` twice here; the `409` arrives with the
behaviour slice. That is the agreed scope (spec §Slice boundary), not a defect — and it is exactly
why checks 1, 3, 8, and 9 matter now: they cover the parts that are real and would be expensive to
get wrong later.
