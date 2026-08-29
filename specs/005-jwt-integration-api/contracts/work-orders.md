# Contract: Work Orders

**Feature**: `005-jwt-integration-api` | Satisfies FR-020 … FR-028

> ⚠️ **Representative responses in this slice.** Shapes, status codes, permissions, and request
> validation are final and implemented. Nothing is written to `work_orders`, and product codes are
> not yet resolved against the real catalogue (FR-033). Every response below — including the ones
> this slice cannot yet produce — is fixed now (FR-034, SC-005).

Requires `Authorization: Bearer <token>`. **Admin or Client** may create a work order; any other
authenticated caller receives `403`.

---

## `POST /api/v1/work-orders`

The point of the feature: a work order names its input and output products **by product code**.

**Request**

```json
{
  "workOrderNumber": "WO-2026-0142",
  "inputProductCode": "RM-STEEL-01",
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
| `inputProductCode` | ✅ | non-blank; must resolve to a product |
| `outputProductCode` | ✅ | non-blank; must resolve to a product |
| `plannedStartTime` | ✅ | valid date-time |
| `qtyToManufacture` | ✅ | **greater than zero** |
| `machineId` | ❌ | positive when present |
| `hourRate` | ❌ | ≥ 0 when present |
| `lineSetupTimeMinutes` | ❌ | ≥ 0 when present |
| `workstationCapabilityPerHour` | ❌ | ≥ 0 when present |

Zero quantity is rejected, not just negative — an order to manufacture nothing is a mistake worth
catching at the edge.

**`201 Created`**

```json
{
  "workOrderNumber": "WO-2026-0142",
  "inputProductCode": "RM-STEEL-01",
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

Both product codes are **echoed as submitted** (FR-027), so a caller confirms what was resolved
without a second call.

`status` is always `"Ready"` on create (FR-026) and travels as a **name**, not the underlying enum
integer. A caller should not have to learn an internal numbering, and that numbering stays free to
change.

---

## Product codes, not record numbers

`input_product_id` and `output_product_id` exist in the database and appear **nowhere** in this
contract, in either direction (FR-022). The caller sends codes; the platform resolves them.

`machineId` is the one internal identifier that remains one. FR-022 constrains *products*; machines
have no external code on this surface, and inventing one is scope this feature was not given. The
asymmetry is deliberate rather than an oversight.

Codes are trimmed and matched case-insensitively, exactly as in [products.md](./products.md).

**The same code may be both input and output.** A rework or re-packing order legitimately consumes
and produces the same product, so this is accepted rather than rejected as a likely typo.

---

## Failures

| Case | Code | Live this slice? |
|---|---|---|
| Missing `workOrderNumber`, either code, or `plannedStartTime` | `400` naming each field | ✅ enforced now |
| `qtyToManufacture` ≤ 0 | `400` | ✅ enforced now |
| Unparsable body | `400` | ✅ enforced now |
| Neither Admin nor Client | `403` | ✅ enforced now |
| No token / expired / deactivated account | `401` | ✅ enforced now |
| `inputProductCode` or `outputProductCode` resolves to no product | `400`, naming **which one** | ❌ specified now, produced by the behaviour slice (US4 §7) |
| `workOrderNumber` already in use | `409` | ❌ specified now, produced by the behaviour slice |

### An unresolvable code is 400, not 404

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "inputProductCode": ["No product exists with code 'RM-999'."] }
}
```

The work-order resource was never addressed — a *field in the body* is wrong. A `404` here would
tell the caller the endpoint is missing. Naming the offending field is what FR-023 requires: when a
two-code request fails, the caller must know which code to fix without guessing.

A code that resolves to a **deactivated** product is accepted. Historical and rework orders
legitimately reference retired materials; a caller wanting only current materials filters the
catalogue itself.

---

## Not in this feature

Starting, holding, resuming, or finishing an order; recording input weights; reading, listing, or
editing existing orders. This surface creates an order in `Ready` and stops there — the lifecycle
stays on the browser screens (FR-026).
