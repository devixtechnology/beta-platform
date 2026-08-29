# Contract: Sign-in, Renewal & Bearer Tokens

**Feature**: `005-jwt-integration-api` | Satisfies FR-001 … FR-010, FR-036 … FR-041

**This surface is fully implemented.** Unlike the product and work-order operations, nothing here is
representative — a real account, a real password check, a real token.

---

## `POST /api/v1/auth/login`

Anonymous. One of the two endpoints in the feature that require no bearer token — the other is
the renewal below, which is anonymous for the same reason: the credential is in the body.

**Request**

```json
{ "email": "admin@beta.local", "password": "Admin@123" }
```

**`200 OK`**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresAt": "2026-08-28T22:14:07Z",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshTokenExpiresAt": "2026-09-27T14:14:07Z",
  "email": "admin@beta.local",
  "fullName": "Beta Administrator",
  "roles": ["Admin"]
}
```

**Failures**

| Case | Code | Notes |
|---|---|---|
| Unknown email, wrong password, or deactivated account | `401` | One identical `"Invalid credentials."` body for all three (FR-004) — see [errors.md](./errors.md) |
| `email` or `password` missing/malformed | `400` | `ValidationProblemDetails` naming the field |

The password policy is **not** applied to the request. A 3-character password is a wrong password
(`401`), not a malformed request (`400`); splitting them would confirm the policy and invite probing.

---

## `POST /api/v1/auth/refresh`

Anonymous — the refresh token **is** the credential, and a caller whose access token has already
expired has nothing else to present. That is the whole point of the endpoint.

**Request**

```json
{ "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." }
```

**`200 OK`** — the **same shape** `/auth/login` returns (FR-040), carrying a new access token *and*
a new refresh token.

**Failures**

| Case | Code | Notes |
|---|---|---|
| Expired, altered, unknown, signed with another key, or an **access** token presented in its place | `401` | One identical `"Invalid or expired refresh token."` body |
| The account was deactivated, deleted, or its password changed since the token was issued | `401` | Same body — a caller learns only that it must sign in again |
| `refreshToken` field missing from the body | `400` | `ValidationProblemDetails` naming the field |

Only **presence** is validated as a request shape. A token that is present but wrong is a rejected
*credential*, not a malformed *request* — the same line `/auth/login` draws between a wrong password
and a missing one, and for the same reason: a `400`/`401` split would tell someone holding a stolen
token which part of it they got wrong.

The token travels in the **body**, never a header or the query string, so it cannot end up in an
access log or a browser history alongside the URL.

### Rotation

Every renewal returns a **new** refresh token. Store it in place of the one you presented; the next
renewal must use the new one.

There is no server-side ledger of issued refresh tokens — this slice adds no schema (constitution
Principle IV), so there is nowhere to keep one. The honest consequence: a refresh token you replace
is not *invalidated*, it stays technically usable until its own 30-day expiry or until the account's
security stamp rotates. What rotation buys here is that the **normal** flow keeps moving the
credential forward, so a token captured from one exchange goes stale as the caller advances. What
revokes a leaked one immediately is deactivating the account or changing its password — see below.

### What a renewal re-reads

| Re-read from the platform at every renewal | Consequence |
|---|---|
| The account exists | A deleted account cannot renew |
| `IsActive` | A deactivated account cannot renew — the refresh window is not a back door around FR-008 |
| The security stamp | A password change or a sign-out-everywhere kills outstanding refresh tokens too |
| The account's **roles** | A role granted or withdrawn since sign-in takes effect on the next renewal, not 30 days later |

This is why the refresh token carries **no role claims**: a role baked into a 30-day credential
would outlive its withdrawal by up to a month. It carries only `sub`, `jti`, `token_use`, the
security stamp, and the standard time claims.

### Why a refresh token cannot be used as an access token, or vice versa

The two are minted with **different audiences** — `BetaPlatformApi` for access,
`BetaPlatformApi.refresh` for renewal. The bearer handler validates the access audience, so a
refresh token presented as `Authorization: Bearer …` is refused before any claim is read; the
renewal endpoint validates the refresh audience, so an access token presented for renewal is refused
the same way. Both also carry a `token_use` claim (`access` / `refresh`) that is checked explicitly,
so the rule is stated in words as well as enforced by construction, and survives someone loosening
an audience check later.

---

## Using the token

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

Every other endpoint requires it. Missing, expired, or unverifiable → `401`; valid but insufficient
role → `403`.

## Lifetime and renewal

- The **access token** lasts **8 hours** from issue — one plant shift (FR-002).
- The **refresh token** lasts **30 days** (`Jwt:RefreshLifetimeDays`) and is rotated on every use
  (FR-037).
- Renew at `POST /auth/refresh`, or by signing in again — both work, and both return the same
  shape (FR-003, FR-036).
- Once the refresh token itself expires, the caller signs in with credentials again. There is no
  credential that never expires.
- `expiresAt` and `refreshTokenExpiresAt` are absolute UTC, so a caller can schedule its own
  renewal without timing the response.
- Clock skew tolerance is **zero** on both, so an 8-hour token expires at 8 hours. The library's
  default five-minute skew would silently make it 8h05m and FR-002 untestable to the minute.

**Which to use.** An unattended integration should hold the refresh token and renew — that way the
account's password does not have to live in its configuration at all. A caller that already has the
password loses nothing by signing in again; both paths are supported deliberately.

## Claims

| Claim | Access token | Refresh token |
|---|---|---|
| `sub` | user id | user id |
| `email`, `name` | attribution | — |
| `role` (repeated) | `Admin` / `Client` — permission decisions need no second look-up (FR-007) | **absent by design**: roles are re-read at renewal |
| `token_use` | `access` | `refresh` |
| `AspNet.Identity.SecurityStamp` | revocation check, below | same check, applied at renewal |
| `aud` | `Jwt:Audience` | `Jwt:Audience` + `.refresh` |
| `jti`, `exp`, `nbf`, `iat`, `iss` | standard | standard |

The token is **signed, not encrypted**. Anyone holding it can read these claims, so nothing secret
goes in one — hence no password hash, no connection detail, and no field a caller could not already
see about itself.

## Immediate revocation (FR-008)

A deactivated account must lose access on its **next request**, not at its 8-hour expiry.

On every API request the bearer handler loads the caller and rejects the token with `401` if the
user is gone, `IsActive` is false, or the stored security stamp no longer matches the claim.
Feature 004 already deactivates an account by rotating that stamp, and `Program.cs` already
re-validates cookies against it every minute — so deactivation means one thing on both doors into
the app rather than a cookie rule and a token rule that drift.

Cost is one primary-key lookup per request. That is the only database work this slice performs.

The same check runs at `POST /auth/refresh`, so a deactivated account cannot renew its way past
deactivation. The refresh window is not a back door around FR-008.

**Changing a password also rotates the stamp**, so it invalidates outstanding access *and* refresh
tokens. That is the correct and expected behaviour, and worth knowing before an integrator reports it
as a bug — it is also the lever to pull if a refresh token is ever believed to have leaked.

## Configuration

| Key | Development | Production |
|---|---|---|
| `Jwt:Issuer` | `BetaPlatform` | set explicitly |
| `Jwt:Audience` | `BetaPlatformApi` | set explicitly |
| `Jwt:SigningKey` | development-only key in `appsettings.Development.json` | **must** be supplied via environment or secret store |
| `Jwt:LifetimeHours` | `8` | `8` |
| `Jwt:RefreshLifetimeDays` | `30` | `30` |

`RefreshLifetimeDays` must be greater than zero **and** longer than `LifetimeHours`: a refresh
token that died before the access token it renews could never be used, and would surface as an
intermittent `401` rather than as the configuration mistake it is. Startup refuses that too.

The app **refuses to start** when the signing key is missing or shorter than 32 bytes, and in
Production also when it matches the development placeholder. This mirrors 004's rule that no default
admin password may exist in Production — a signing key is the same class of secret with a worse
blast radius, since whoever holds it mints tokens for any account and any role. Failing at startup
makes that loud; failing at first sign-in would look like a puzzling runtime bug.

`appsettings.json` carries the section with an **empty** `SigningKey` so the shape is discoverable
without shipping a usable secret.

## Coexistence with the browser sign-in

The JWT handler is registered as an **additional, non-default** scheme; Identity's cookie scheme
remains the default and every existing screen behaves exactly as before (SC-008). Each API controller
names the bearer scheme explicitly, which is what makes an unauthenticated API call answer a bare
`401` instead of redirecting to `/Auth/Login` (FR-009).

Cookies are never accepted on `api/v1/*`, and a token is never accepted on the MVC screens. The two
doors do not overlap, and neither can be used to reach the other.
