# Feature Specification: An administrator's console

> Completed on 2026-09-27, after the feature merged (#82, with its follow-up fix #83), from the code at
> that merge, the pull requests and docs/features/fulfilment-and-delivery.md.

**Feature branch**: `038-admin-console`
**Created**: 2026-09-23
**Status**: Merged (#82 on 2026-09-23; the dialog fix it found, #83, the same day)
**Issue**: none - neither pull request closes an issue.

## What is wrong

Everything an administrator does after a sale - preparing and shipping the shop's own parcels
(specs/011, 035), settling what sellers are owed (specs/037) - exists only as API endpoints, reached
through Bruno. The storefront has pages for customers and for sellers and none for the people who run
the shop.

And one of those jobs cannot be done even through the API: the fulfilment queue lists orders, but
**nothing lets an administrator read an order** - `GET /api/orders/{id}` answers the order's owner only.
Staff can mark an order shipped without any way to see what is in it or where it goes.

## User Scenarios & Testing

### US1 - Staff work the shop's parcels (Priority: P1)

**Why this priority**: shipping without seeing the order is the one job that cannot be done even
through the API.

**Independent Test**: as an administrator in the storefront, take a paid order holding the shop's goods
and a seller's from the waiting list to shipped, and see the seller's parcel left alone.

**Acceptance Scenarios**:

1. **Given** paid orders, **When** an administrator opens the console, **Then** they see the orders whose
   shop parcel is waiting, being prepared, or shipped - one list per state, a page at a time, oldest
   first (the queue order the server already uses).
2. **Given** one order in the queue, **When** it is opened, **Then** it shows what the shop has to pack,
   where it goes, and every parcel of the order - so staff can see that a seller's parcel is somebody
   else's job.
3. **Given** the shop's parcel is waiting, **When** staff act on it, **Then** they start preparing it, then
   ship it with a tracking reference - the same two steps a seller takes on their own parcel.
4. **Given** an order with no shop goods, **When** it is opened, **Then** it says that each seller ships
   their own, and offers no action.

---

### US2 - Staff settle what sellers are owed (Priority: P1)

**Why this priority**: the payout ledger (specs/037) is useless to somebody who does not use Bruno.

**Independent Test**: with a seller owed money, record the payout from the console and see the list
update and the recorded amount confirmed.

**Acceptance Scenarios**:

1. **Given** sellers are owed money, **When** an administrator opens payouts, **Then** they see every
   seller with something due, per currency, with the shop's name.
2. **Given** a seller in that list, **When** the administrator records a payout, **Then** they first
   confirm the seller and the amount, and the list updates.
3. **Given** someone else settled it first, **When** the administrator records it, **Then** they are told
   nothing is due, in the server's words.

---

### US3 - Only staff are offered it (Priority: P1)

**Why this priority**: the new read is the one order read not scoped to its owner; drawing the console
for others would invite the attempt, and the server must refuse it regardless.

**Independent Test**: sign in as a customer and as a seller - no console link; call the staff order read
with their tokens - 403.

**Acceptance Scenarios**:

1. **Given** a signed-in person who is not an administrator, **When** the storefront is drawn, **Then** the
   console and the way to it are not drawn. It remains the server that refuses everyone else.

---

### Edge Cases

- **An order with no shop goods.** No action; "each seller ships their own".
- **An order an older image wrote, with no parts.** The whole order is the shop's parcel, in the order's
  own state (specs/035 D3).
- **Settled by someone else first.** The server's 409 is shown in its own words; the dialog must have
  closed so it is visible (the defect #83 fixed).
- **More due than the list showed.** The server pays what is due at that moment; the toast shows the
  amount actually recorded.
- **A phone.** The console fits at 390 px with no horizontal overflow.

## Requirements

### Functional Requirements

- **FR-001** Staff MUST be able to read any order's detail through an Admin-only endpoint.
- **FR-002** The console MUST offer only the next step of the shop's parcel, as the server enforces.
- **FR-003** A payout MUST be confirmed before it is recorded, naming the seller and the amount.
- **FR-004** The console's text MUST be in Vietnamese and English, like the rest of the storefront.

### Key Entities

No new entity. The console reads the order (with its parts, specs/035) and the payouts due (specs/037).

## Out of scope

- Moderating products and categories, payments and reservations audit, user management.
- A per-order payout, or paying less than what is due.

## Success Criteria

- **SC-001** An administrator can take a paid order from waiting to shipped without leaving the storefront.
- **SC-002** An administrator can settle a seller without Bruno.
- **SC-003** A customer or a seller calling the order endpoint for staff gets 403.

## Assumptions

*(Added in this backfill.)*

- "Staff" here means `Admin`: specs/043 later introduced `Moderator`, and the fulfilment and payout
  endpoints stayed `Admin`.
- The client learns the role from `roles` on the authentication response (specs/028) - for drawing
  only.
