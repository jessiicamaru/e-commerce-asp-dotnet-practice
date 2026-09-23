# Feature Specification: An administrator's console

**Feature branch**: `038-admin-console`
**Created**: 2026-09-23
**Status**: Draft

## What is wrong

Everything an administrator does after a sale - preparing and shipping the shop's own parcels
(specs/011, 035), settling what sellers are owed (specs/037) - exists only as API endpoints, reached
through Bruno. The storefront has pages for customers and for sellers and none for the people who run
the shop.

And one of those jobs cannot be done even through the API: the fulfilment queue lists orders, but
**nothing lets an administrator read an order** - `GET /api/orders/{id}` answers the order's owner only.
Staff can mark an order shipped without any way to see what is in it or where it goes.

## User Scenarios

### US1 - Staff work the shop's parcels (P1)

**Acceptance**
1. An administrator sees the orders whose shop parcel is waiting, being prepared, or shipped - one
   list per state, a page at a time, oldest first (the queue order the server already uses).
2. Opening one shows what the shop has to pack, where it goes, and every parcel of the order - so
   staff can see that a seller's parcel is somebody else's job.
3. From there they start preparing it, then ship it with a tracking reference - the same two steps a
   seller takes on their own parcel.
4. An order with no shop goods says that each seller ships their own, and offers no action.

### US2 - Staff settle what sellers are owed (P1)

**Acceptance**
1. An administrator sees every seller with something due, per currency, with the shop's name.
2. They record a payout after confirming the seller and the amount; the list updates.
3. When someone else settled it first, they are told nothing is due, in the server's words.

### US3 - Only staff are offered it (P1)

**Acceptance**
1. The console and the way to it are drawn only for an administrator. It remains the server that
   refuses everyone else.

## Requirements

- **FR-001** Staff MUST be able to read any order's detail through an Admin-only endpoint.
- **FR-002** The console MUST offer only the next step of the shop's parcel, as the server enforces.
- **FR-003** A payout MUST be confirmed before it is recorded, naming the seller and the amount.
- **FR-004** The console's text MUST be in Vietnamese and English, like the rest of the storefront.

## Out of scope

- Moderating products and categories, payments and reservations audit, user management.
- A per-order payout, or paying less than what is due.

## Success Criteria

- **SC-001** An administrator can take a paid order from waiting to shipped without leaving the storefront.
- **SC-002** An administrator can settle a seller without Bruno.
- **SC-003** A customer or a seller calling the order endpoint for staff gets 403.
