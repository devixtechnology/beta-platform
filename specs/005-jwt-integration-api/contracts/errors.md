# Contract: Error Shape & Status Codes

**Feature**: `005-jwt-integration-api` | Satisfies FR-006, FR-029, FR-030, FR-031, FR-035

One error shape across all six operations, so a caller writes one error handler.

## The shape — RFC 9457 `ProblemDetails`

Media type `application/problem+json`.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "productCode": ["The ProductCode field is required."],
    "unit": ["The field Unit must be a string with a maximum length of 20."]
  },
  "traceId": "00-f20b04070f7eb698db0112b95671e724-7f73fcf7dfbcd155-00"
}
```

The **keys** are the camel-case field names the caller sent, so the errors dictionary matches the
body it is complaining about. (This does not happen by default — ASP.NET Core keys validation errors
by the CLR property name, so the API registers a `SystemTextJsonValidationMetadataProvider` to line
the two up. Without it a client keying off `productCode` would be handed `ProductCode`.) The message
*text* still names the CLR property; branch on the key, never on the sentence.

`traceId` is added by the framework to every failure. It is a correlation id for matching a response
against the server log — not internal diagnostics, and it reveals nothing about the platform.

`errors` is present **only** on `400` validation failures; it names each offending field, which is
what FR-030 and the "naming each offending field" acceptance scenarios require. Every other failure
carries `type`, `title`, `status` and `traceId` alone:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "A product with this code already exists.",
  "status": 409,
  "traceId": "00-24c09efbca397009d0b23b37a5f2cf94-3a57cf72ba18514e-00"
}
```

**Nothing else may appear.** No stack trace, no database message, no internal record number
(FR-031). `ProblemDetails` has no field one could travel in by accident, which is half the reason it
was chosen; the other half is that `[ApiController]`'s automatic validation response already uses
it, so the hand-written and automatic paths agree without extra configuration.

## The `type` URLs

Produced by ASP.NET Core's own client-error mapping, so they are the same on every response the
framework and this feature generate:

| Status | `type` |
|---|---|
| 400 | `https://tools.ietf.org/html/rfc9110#section-15.5.1` |
| 401 | `https://tools.ietf.org/html/rfc9110#section-15.5.2` |
| 403 | `https://tools.ietf.org/html/rfc9110#section-15.5.4` |
| 404 | `https://tools.ietf.org/html/rfc9110#section-15.5.5` |
| 409 | `https://tools.ietf.org/html/rfc9110#section-15.5.10` |
| 500 | `https://tools.ietf.org/html/rfc9110#section-15.6.1` |

## Status-code map

Each outcome has exactly one code, so a caller branches on the code and never parses message text
(SC-003).

| Code | Meaning on this surface | Body |
|---|---|---|
| `200 OK` | read succeeded | resource / list |
| `201 Created` | product or work order created; `Location` header set | created resource |
| `400 Bad Request` | request shape is wrong — missing field, too long, `qty <= 0`, unparsable body, unresolvable product code | `ValidationProblemDetails` |
| `401 Unauthorized` | no token, expired token, bad signature, an account deactivated since issue, or a refused refresh token | `ProblemDetails` |
| `403 Forbidden` | valid token, insufficient role | `ProblemDetails` |
| `404 Not Found` | product code matches nothing | `ProblemDetails` |
| `409 Conflict` | product code or work-order number already in use | `ProblemDetails` |
| `500 Internal Server Error` | unhandled fault | bare `ProblemDetails`, no detail |

### 401 vs 403 (FR-006)

The distinction is load-bearing and is the framework's own: **challenge → 401** (we do not know who
you are), **forbid → 403** (we know, and you may not). A caller uses it to decide between signing in
again and alerting a human.

An unauthenticated API call must **never** answer `302` to `/Auth/Login`. The cookie handler would do
exactly that, which is why each API controller names the bearer scheme explicitly (research R1).
This is the single most likely thing to break in this feature, and `quickstart.md` checks it first.

### Why an unresolvable product code is 400, not 404

`GET /products/{code}` for an unknown code is `404` — the *addressed resource* does not exist.
`POST /work-orders` naming an unknown input code is `400` — the work-order resource was never
addressed; a *field in the request body* is wrong. Answering `404` there would tell a caller the
endpoint is missing. The `errors` dictionary names which of the two codes failed (FR-023):

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "inputProductCode": ["No product exists with code 'RM-999'."] }
}
```

## Expired vs malformed tokens

Both are `401`, so the body cannot distinguish them without leaking. The difference travels in the
`WWW-Authenticate` header, which is where an integration can act on it automatically:

```
WWW-Authenticate: Bearer error="invalid_token", error_description="The token expired at '...'"
```

Expired → sign in again. Malformed → a real fault worth surfacing. Without this an integration
cannot tell "renew" from "page someone", which is the first Edge Case in the spec.

## Sign-in failures say as little as possible

Unknown account, wrong password, and deactivated account all return the **same** `401` with the same
title (FR-004):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Invalid credentials.",
  "status": 401
}
```

One nuance worth knowing: the 401 and 403 produced by the bearer handler itself (no token, expired
token, insufficient role) are written outside MVC and so carry `type`, `title` and `status` only —
no `traceId`. Every failure reaching a controller carries one.

A distinct "this account is deactivated" message would be kinder — and the browser login page does
show one, deliberately, because a person retyping a correct password deserves to know. An
unauthenticated API caller is not that person: on this surface the message is an account-existence
oracle for anyone with a list of emails. The two surfaces differ on purpose.

A missing or malformed `email`/`password` field is a `400` — that is a broken request, not a failed
credential, and it reveals nothing.

## Renewal failures say as little as possible, for the same reason

`POST /auth/refresh` answers every failure with one identical `401`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Invalid or expired refresh token.",
  "status": 401
}
```

Expired, altered, signed with another key, an **access** token presented in its place, an account
that has since been deleted, deactivated, or had its password changed — all one body. Splitting them
would tell whoever holds a stolen token exactly what is wrong with it, and would turn the endpoint
into the account-existence oracle sign-in refuses to be (FR-040).

A missing `refreshToken` **field** is a `400`, exactly as a missing `password` is. A *present but
wrong* token is a `401`: it is a rejected credential, not a broken request.

| Failure | Code |
|---|---|
| `refreshToken` field absent from the body | `400` |
| Anything wrong with the token itself, or with the account behind it | `401` |

## Language

Messages are English literals, produced independently of the request-localisation middleware
(FR-035, research R10). The app resolves culture from a cookie only; an API caller sends none, so
localisation would decide nothing while making messages depend on a cookie no machine sets. Callers
branch on the status code, never on the text.
