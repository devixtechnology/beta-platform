# Contract: Products

**Feature**: `005-jwt-integration-api` | Satisfies FR-011 … FR-019, FR-022

> ⚠️ **Representative responses in this slice.** The shapes, status codes, permissions, and
> request validation below are final and implemented. The *data* is sample data: nothing is read
> from or written to the `products` table yet (FR-033). Every response documented here — including
> the `409` that this slice cannot yet produce — is fixed now and will not change when the behaviour
> is wired (FR-034, SC-005).

All three operations require `Authorization: Bearer <token>`.

## The product shape

One representation for reads and creates alike (FR-019), so a caller writes a single parser:

```json
{
  "productCode": "RM-STEEL-01",
  "productName": "لفائف صلب",
  "productNameEnglish": "Steel Coil",
  "category": "Raw Material",
  "unit": "kg",
  "isActive": true
}
```

**There is no `productId`.** The internal record number is absent by design (FR-022) — a caller
never needs it and is never given it. Its absence is the promise of this feature, not an oversight.

Both names always travel; the caller picks which to display. Lengths match the stored columns
exactly: code ≤ 50, names ≤ 200, category ≤ 100, unit ≤ 20.

---

## `GET /api/v1/products`

Any authenticated caller. Returns the catalogue.

**Query**

| Parameter | Type | Default | Meaning |
|---|---|---|---|
| `activeOnly` | boolean | `false` | exclude deactivated products |

**`200 OK`** — a JSON array of product shapes. An empty catalogue is `[]`, never `404`.

No pagination in this slice: the catalogue is hundreds of rows, not millions. Adding it later is an
additive query parameter, not a shape change.

`activeOnly` is part of the contract now even though it selects from sample data — a client that
handles the filter today keeps working when the filter starts filtering.

---

## `GET /api/v1/products/{productCode}`

Any authenticated caller. Fetches one product **by code** — the code the plant prints and files by.

**`200 OK`** — one product shape.

**`404 Not Found`** — no product carries that code. **Produced today**: the representative catalogue
is finite, so a code outside it genuinely answers 404. (Contrast the `409` on create, which needs
stored data to detect and therefore waits for the behaviour slice.)

A **deactivated** product is returned normally with `isActive: false`; it is not reported missing. A
caller reconciling history needs to see retired materials, and hiding them behind a `404` would make
"never existed" and "no longer used" indistinguishable.

### Code matching

Trimmed of surrounding whitespace, compared **case-insensitively** — `rm-steel-01`, `RM-STEEL-01`,
and `  RM-Steel-01  ` all address the same product.

Fixed now on purpose. MySQL's default collation is already case-insensitive, so an implementation
written without thinking would match that way *by accident* on the server while any in-memory check
matched case-sensitively — a discrepancy that surfaces as failures nobody can reproduce. The rule is
one shared helper on both sides.

---

## `POST /api/v1/products`

**Admin only.** A `Client` token receives `403` (data-model.md authorization map).

**Request**

```json
{
  "productCode": "RM-STEEL-02",
  "productName": "لفائف صلب رفيعة",
  "productNameEnglish": "Thin Steel Coil",
  "category": "Raw Material",
  "unit": "kg"
}
```

| Field | Required | Rules |
|---|---|---|
| `productCode` | ✅ | non-blank after trim; ≤ 50 |
| `productName` | ✅ | non-blank; ≤ 200 |
| `productNameEnglish` | ❌ | ≤ 200 |
| `category` | ❌ | ≤ 100 |
| `unit` | ✅ | non-blank; ≤ 20 |

`isActive` is **not accepted** — a created product is active (FR-017). Offering a field the platform
overrides would invite a caller to believe otherwise. `createdAt` is likewise server-assigned.

Unknown extra fields are ignored rather than refused, so a caller running slightly ahead of the
contract is not broken by it.

**`201 Created`** — the created product in the same shape the reads return, with
`Location: /api/v1/products/{productCode}`.

**Failures**

| Case | Code | Live this slice? |
|---|---|---|
| Missing field, over-length, unparsable body | `400` naming each field | ✅ enforced now |
| `Client` or other non-admin token | `403` | ✅ enforced now |
| No token / expired / deactivated account | `401` | ✅ enforced now |
| Unknown code on `GET /{productCode}` | `404` | ✅ produced now — the sample catalogue is finite |
| Product code already in use | `409` | ❌ specified now, produced by the behaviour slice (US3 §5) |

The `409` is a conflict, not a validation failure: the request is well-formed, the *world* disagrees
with it. Keeping the two apart lets a caller retry with a new code on `409` and fix its payload on
`400` without reading a message.

---

## Not in this feature

Update, deactivate, and delete. The API is read-and-add only; changing or retiring a product stays
on the browser screens.
