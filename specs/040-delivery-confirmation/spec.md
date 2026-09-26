# Feature Specification: Confirming a parcel arrived

> Completed on 2026-09-27, after the feature merged (#85), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Feature branch**: `040-delivery-confirmation`
**Created**: 2026-09-24
**Status**: Merged (#85, 2026-09-23 UTC)
**Issue**: none - the pull request closes no issue.

## What is wrong

Since specs/037 a seller's money becomes **due the moment their parcel is marked shipped** - the last
thing the system knew. A seller who marks a parcel shipped and never sends it is owed, and paid, all the
same. Nothing records that a parcel arrived, and the customer has no way to say it did - or to hold back
saying so when it did not.

## Decisions taken with the user (2026-09-24)

- **The customer confirms each parcel** ("Đã nhận hàng").
- **A parcel nobody confirms is taken as delivered 7 days after it shipped** - configurable - so a
  seller is not left unpaid by a customer who simply never clicks.

## User Scenarios & Testing

### US1 - The customer confirms a parcel arrived (Priority: P1)

**Why this priority**: the customer is the only one who knows; without their word "delivered" is only the
seller's claim.

**Independent Test**: as the customer of a shipped order, confirm one parcel; it reads received, a repeat
changes nothing, and another customer's attempt is "not found".

**Acceptance Scenarios**:

1. **Given** a shipped parcel on their order, **When** the customer chooses "I've received it" and confirms,
   **Then** it is marked received, once.
2. **Given** a parcel not shipped yet, **When** the customer tries to confirm it, **Then** it cannot be
   confirmed; **Given** someone else's parcel, **When** they try, **Then** it is "not found".
3. **Given** every parcel of an order is received, **When** the order is read, **Then** it reads as
   delivered.

---

### US2 - A seller is paid for what arrived (Priority: P1)

**Why this priority**: it is the defect - money due for a parcel that may never have been sent.

**Independent Test**: ship a seller's parcel; the balance reads "on the way" and the due list does not
include it; confirm it; it becomes due and a payout can claim it.

**Acceptance Scenarios**:

1. **Given** a shipped parcel, **When** the seller's balance is read, **Then** its money stays **on the
   way** until it is delivered, then becomes **due**.
2. **Given** shipped parcels, some delivered, **When** a payout is recorded, **Then** it claims delivered
   parcels only.
3. **Given** the customer confirmed, **When** the seller reads the sale, **Then** they see that their parcel
   has been received, and when.

---

### US3 - Nobody waits forever (Priority: P1)

**Why this priority**: without it a customer who never clicks leaves a seller unpaid for ever, which is
the same defect in the other direction.

**Independent Test**: a parcel shipped more than the period ago with no confirmation becomes delivered at
the next sweep, recorded as automatic; a second sweep changes nothing.

**Acceptance Scenarios**:

1. **Given** a parcel shipped more than 7 days ago that nobody confirmed, **When** the sweep runs, **Then**
   it becomes delivered on its own, recorded as confirmed automatically rather than by the customer.
2. **Given** the period is configuration, **When** Order starts with a period that is not sensible, **Then**
   it refuses to start.
3. **Given** the sweep has run, **When** it runs twice, or on two instances at once, **Then** nothing
   changes the second time.

---

### Edge cases

- **Parcels shipped before this feature** have no shipped time; the migration takes it from when the
  parcel last changed, which for a shipped parcel is when it shipped. They are therefore auto-confirmed
  at the first sweep if older than 7 days - money that was "due" under specs/037 stays due, or is due
  within the week.
- **A cancelled order** never has a shipped parcel (specs/039), so there is nothing to confirm.
- **A customer confirming while the sweep runs**: whoever is first is recorded; the other affects nothing.
- **Parts created on demand for an older image's order** in `Shipped` get a shipped time too (the order's
  `UpdatedAt`), so the sweep can reach them.

## Requirements

### Functional Requirements

- **FR-001** Delivery is recorded per parcel, with when and by whom ("Customer" or "Auto").
- **FR-002** Only a shipped parcel of the caller's own order can be confirmed; a repeat is a no-op.
- **FR-003** Money is due only for delivered parcels.
- **FR-004** Unconfirmed parcels are delivered automatically after the configured period after shipping.
- **FR-005** No status value an older image cannot parse.

### Key Entities

- **Shipment part**: gains when it shipped, when it was delivered, and who said so - the customer, or
  "Auto" for the sweep. Still `Shipped` in status; delivered is a fact beside the status, not a new one.

## Out of scope

- Disputes ("I never received it"), returns, carrier tracking integration.

## Success Criteria

- **SC-001** No payout ever includes a parcel that is neither confirmed nor older than the period.
- **SC-002** `verify-saga.sh` confirms the parcel it ships and passes.

## Assumptions

*(Added in this backfill.)*

- Nothing moves a part after it ships (specs/035), so a shipped part's `UpdatedAt` is when it shipped -
  what the backfill relies on.
- One sweep per hour is often enough for a seven-day period; both are configuration.
