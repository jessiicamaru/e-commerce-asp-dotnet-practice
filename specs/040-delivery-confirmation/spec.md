# Feature Specification: Confirming a parcel arrived

**Feature branch**: `040-delivery-confirmation`
**Created**: 2026-09-24
**Status**: Draft

## What is wrong

Since specs/037 a seller's money becomes **due the moment their parcel is marked shipped** - the last
thing the system knew. A seller who marks a parcel shipped and never sends it is owed, and paid, all the
same. Nothing records that a parcel arrived, and the customer has no way to say it did - or to hold back
saying so when it did not.

## Decisions taken with the user (2026-09-24)

- **The customer confirms each parcel** ("Đã nhận hàng").
- **A parcel nobody confirms is taken as delivered 7 days after it shipped** - configurable - so a
  seller is not left unpaid by a customer who simply never clicks.

## User Scenarios

### US1 - The customer confirms a parcel arrived (P1)

**Acceptance**
1. On their order, a shipped parcel offers "I've received it"; confirming marks it received, once.
2. A parcel not shipped yet cannot be confirmed; someone else's parcel is "not found".
3. The order reads as delivered once every parcel is.

### US2 - A seller is paid for what arrived (P1)

**Acceptance**
1. A shipped parcel's money stays **on the way** until it is delivered, then becomes **due**.
2. A payout claims delivered parcels only.
3. A seller sees whether their parcel has been received, and when.

### US3 - Nobody waits forever (P1)

**Acceptance**
1. A parcel shipped more than 7 days ago that nobody confirmed becomes delivered on its own, recorded as
   confirmed automatically rather than by the customer.
2. The period is configuration; Order refuses to start without a sensible one.
3. Running the sweep twice, or on two instances at once, changes nothing the second time.

### Edge cases

- **Parcels shipped before this feature** have no shipped time; the migration takes it from when the
  parcel last changed, which for a shipped parcel is when it shipped. They are therefore auto-confirmed
  at the first sweep if older than 7 days - money that was "due" under specs/037 stays due, or is due
  within the week.
- **A cancelled order** never has a shipped parcel (specs/039), so there is nothing to confirm.

## Requirements

- **FR-001** Delivery is recorded per parcel, with when and by whom ("Customer" or "Auto").
- **FR-002** Only a shipped parcel of the caller's own order can be confirmed; a repeat is a no-op.
- **FR-003** Money is due only for delivered parcels.
- **FR-004** Unconfirmed parcels are delivered automatically after the configured period after shipping.
- **FR-005** No status value an older image cannot parse.

## Out of scope

- Disputes ("I never received it"), returns, carrier tracking integration.

## Success Criteria

- **SC-001** No payout ever includes a parcel that is neither confirmed nor older than the period.
- **SC-002** `verify-saga.sh` confirms the parcel it ships and passes.
