# Specification Quality Checklist: Integration API Behaviour

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-11
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`.

### Iteration 1 — 2026-09-11

Three `[NEEDS CLARIFICATION]` markers were raised, all at the top of the impact ordering, because
each one either shapes stored data or changes what a caller sees:

| # | Requirement | Why it could not be defaulted |
|---|---|---|
| Q1 | FR-025 | Whether the order's existing single input reference stays "primary" or is superseded decides the migration, the backfill, and whether the running-orders reporting view survives. |
| Q2 | FR-028 | Accepting versus refusing a deactivated code changes what a caller may legitimately submit, and the two defensible answers point opposite ways: the work-order screen offers only active products, while the fetch operation deliberately returns deactivated ones. |
| Q3 | FR-024 | Whether the browser screens learn about several inputs is a scope decision with real cost, and getting it wrong means an order that shows less of itself than it carries. |

### Iteration 2 — 2026-09-11 — all items pass

Answered by the author and written into **Clarifications**, FR-024 … FR-029, US3 scenarios 9-10,
US4 scenarios 3-7, SC-009, SC-010, Assumptions and Out of Scope:

- **Q1 → primary plus full list.** The single reference keeps the first input and stays required;
  the new record holds the complete list including the first entry; every existing order is
  backfilled with one entry. No existing screen or view changes. SC-010 makes the invariant checkable.
- **Q2 → refuse a deactivated code for new orders**, naming the same field an unknown code names,
  identically for inputs and the output — while reading a deactivated product by code still returns
  it (FR-004, FR-029).
- **Q3 → viewing only.** The order-details screen shows every input; the create and edit forms are
  untouched. Multi-input editing is named explicitly in Out of Scope so it is a decision on record
  rather than an omission.

Structural verification: FR-001 … FR-037 and SC-001 … SC-010 are contiguous with no gaps or
duplicates; zero clarification markers remain; every mandatory section is present.

Everything else was defaulted and recorded under **Assumptions** rather than raised as a question —
notably the absence of pagination, the absence of any read-back endpoint for work orders, and
routing the API through the platform's existing product and work-order services rather than writing
a second set of rules that could drift from the screens'.
