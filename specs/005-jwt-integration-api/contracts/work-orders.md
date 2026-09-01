# Contract: Work Orders

**Feature**: `005-jwt-integration-api` | Satisfies FR-020 … FR-028, FR-042 … FR-044

> ⚠️ **Representative responses in this slice.** Shapes, status codes, permissions, and request
> validation are final and implemented. Nothing is written to `work_orders`, and product codes are
> not yet resolved against the real catalogue (FR-033). Every response below — including the ones
> this slice cannot yet produce — is fixed now (FR-034, SC-005).

> 📝 **Amended 2026-09-01, at the author's request.** The input side is a **list** of product codes;
> the output side remains a **single** code. See research R13 for the reasoning and for what it costs
> the behaviour slice.

Requires `Authorization: Bearer <token>`. **Admin or Client** may create a work order; any other
authenticated caller receives `403`.

---

## `POST /api/v1/work-orders`

The point of the feature: a work order names its input and output products **by product code**.

**Request**

```json
{
  "workOrderNumber": "WO-2026-0142",
  "inputProductCodes": ["RM-STEEL-01", "RM-PAINT-02"],
  "outputProductCode": "FG-PANEL-07",
  "plannedStartTime": "2026-08-29T06:00:00",
  "qtyToManufacture": 1200.5,
  "machineId": 3,
  "hourRate": 45.0,
  "lineSetupTimeMinutes": 30,
  "workstationCapabilityPerHour": 150.0
}
```

| Field | Required | Rules |
|---|---|---|
| `workOrderNumber` | ✅ | non-blank; ≤ 50 |
| `inputProductCodes` | ✅ | array of strings; **≥ 1 entry**; no blank entry; **no code twice**; each must resolve to a product |
| `outputProductCode` | ✅ | non-blank; must resolve to a product |
| `plannedStartTime` | ✅ | valid date-time |
| `qtyToManufacture` | ✅ | **greater than zero** |
| `machineId` | ❌ | positive when present |
| `hourRate` | ❌ | ≥ 0 when present |
| `lineSetupTimeMinutes` | ❌ | ≥ 0 when present |
| `workstationCapabilityPerHour` | ❌ | ≥ 0 when present |

An order consuming one material sends a one-entry array — the list is the shape, not a special case:

```json
{ "inputProductCodes": ["RM-STEEL-01"], "outputProductCode": "FG-PANEL-07", "…": "…" }
```

Zero quantity is rejected, not just negative — an order to manufacture nothing is a mistake worth
catching at the edge. An **empty** input array is rejected for the same reason: an order that
consumes nothing is the same class of mistake, and `[]` is far likelier to be a client that built its
list wrongly than a deliberate statement.

**`201 Created`**

```json
{
  "workOrderNumber": "WO-2026-0142",
  "inputProductCodes": ["RM-STEEL-01", "RM-PAINT-02"],
  "outputProductCode": "FG-PANEL-07",
  "status": "Ready",
  "plannedStartTime": "2026-08-29T06:00:00",
  "qtyToManufacture": 1200.5,
  "machineId": 3,
  "hourRate": 45.0,
  "lineSetupTimeMinutes": 30,
  "workstationCapabilityPerHour": 150.0
}
```

Every code is **echoed as submitted** (FR-027) and the inputs come back **in the order sent**, so a
caller can match the response against its own list position by position without re-sorting it.

`status` is always `"Ready"` on create (FR-026) and travels as a **name**, not the underlying enum
integer. A caller should not have to learn an internal numbering, and that numbering stays free to
change.

---

## Why the inputs are a list and the output is not

An order consumes several materials — steel *and* paint *and* fixings — and produces one end product.
That asymmetry is the plant's, not the API's, so the contract states it rather than flattening it:
`inputProductCodes` is an array, `outputProductCode` is a string. Pluralising the output as well
would invent a shape nobody works in and force every caller to unwrap a one-element array forever.

**No quantity per input.** An input names *what* is consumed, not how much; `qtyToManufacture` is the
order's output quantity and stays the only quantity in the contract. Consumption per material is a
bill-of-materials concept this feature was not given, and offering a field the platform would ignore
is worse than leaving it out (FR-043).

**No code twice.** Because an input carries no quantity, listing the same code twice says nothing a
single entry does not — it can only be a client-side mistake, so it is refused at the edge rather
than silently collapsed. Comparison is trimmed and case-insensitive, exactly as everywhere else.

---

## Product codes, not record numbers

`input_product_id` and `output_product_id` exist in the database and appear **nowhere** in this
contract, in either direction (FR-022). The caller sends codes; the platform resolves them.

`machineId` is the one internal identifier that remains one. FR-022 constrains *products*; machines
have no external code on this surface, and inventing one is scope this feature was not given. The
asymmetry is deliberate rather than an oversight.

Codes are trimmed and matched case-insensitively, exactly as in [products.md](./products.md).

**The output code may repeat one of the inputs.** A rework or re-packing order legitimately consumes
and produces the same product, so this is accepted rather than rejected as a likely typo. The
no-repeats rule governs the input list *among itself*; input and output are two different fields.

---

## Failures

| Case | Code | Live this slice? |
|---|---|---|
| Missing `workOrderNumber`, `outputProductCode`, or `plannedStartTime` | `400` naming each field | ✅ enforced now |
| `inputProductCodes` absent, `null`, or `[]` | `400` naming `inputProductCodes` | ✅ enforced now |
| A blank entry in `inputProductCodes` | `400` naming `inputProductCodes`, quoting the position | ✅ enforced now |
| The same code twice in `inputProductCodes` | `400` naming `inputProductCodes`, quoting the code | ✅ enforced now |
| `qtyToManufacture` ≤ 0 | `400` | ✅ enforced now |
| Unparsable body | `400` | ✅ enforced now |
| Neither Admin nor Client | `403` | ✅ enforced now |
| No token / expired / deactivated account | `401` | ✅ enforced now |
| An entry of `inputProductCodes` resolves to no product | `400`, naming **`inputProductCodes[i]`** at the position submitted | ❌ specified now, produced by the behaviour slice (US4 §7) |
| `outputProductCode` resolves to no product | `400`, naming `outputProductCode` | ❌ specified now, produced by the behaviour slice |
| `workOrderNumber` already in use | `409` | ❌ specified now, produced by the behaviour slice |

### An unresolvable code is 400, and it names the entry

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "inputProductCodes[1]": ["No product exists with code 'RM-999'."] }
}
```

The work-order resource was never addressed — a *field in the body* is wrong. A `404` here would
tell the caller the endpoint is missing. Naming the offending field is what FR-023 requires, and with
a list that means naming the **index**: a caller sending six codes must know which one to fix without
diffing its own payload against the response.

The index is the caller's own — position `0` is the first code it sent. Several unresolvable codes
may be reported together, one key per position, since the `errors` dictionary already carries many.

A code that resolves to a **deactivated** product is accepted. Historical and rework orders
legitimately reference retired materials; a caller wanting only current materials filters the
catalogue itself.

---

## Not in this feature

Starting, holding, resuming, or finishing an order; recording input weights; reading, listing, or
editing existing orders. This surface creates an order in `Ready` and stops there — the lifecycle
stays on the browser screens (FR-026).

Persisting more than one input product is likewise **not** in this feature: `work_orders` holds a
single `input_product_id`, and 005 adds no migration (research R13). Nothing is written here, so the
contract is free to state the shape the platform will grow into; the behaviour slice carries the
schema change that makes it storable.
