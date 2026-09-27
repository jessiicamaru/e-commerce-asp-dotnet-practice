# Feature Specification: A banned seller's shop is closed

**Feature Branch**: `095-suspended-seller` | **Created**: 2026-09-27 | **Issue**: #193

**Status**: Draft

**Input**: Issue #193 - "a banned seller's products stay on sale".

## Why

An administrator bans a seller (specs/043): the person cannot sign in, every session ends, every token is refused
within seconds (specs/065). Their products stay on the shelf. Shoppers keep buying them; the orders become the banned
seller's sales, and nobody ships them - the seller cannot sign in, and staff move only the shop's own parcel
(specs/035).

Catalog never hears of a ban. It consumes Identity's `SellerRegisteredEvent` and `SellerRenamedEvent` for the shop-name
read model and nothing else, and "on the shelf" (`Product.OnShelf`, specs/092) knows nothing of the seller.

## User Scenarios & Testing *(mandatory)*

### US1 - Banning a seller takes their shop off the shelf (Priority: P1)

When an administrator bans somebody who sells, every product of theirs leaves the shelf within seconds: not listed,
the same 404 as a product taken down (specs/081, 092), and refused at checkout.

**Why this priority**: It is the defect.

**Independent Test**: A seller with a product on sale is banned; Catalog, having consumed the announcement, lists the
product nowhere, answers 404 to a shopper, and prices it unsellable.

**Acceptance Scenarios**:

1. **Given** a seller with products on sale, **When** they are banned, **Then** Identity announces the shop suspended
   in the ban's own transaction.
2. **Given** that announcement, **When** Catalog records it, **Then** each of the seller's products is off the shelf:
   absent from the listing, 404 at its address for shoppers, `Sellable = false` to checkout.
3. **Given** the seller's products off the shelf, **Then** staff and the seller themselves (were they able to sign in)
   still see them, as for any product off the shelf.
4. **Given** a banned person who is not a seller, **Then** nothing is announced and Catalog changes nothing.

---

### US2 - Lifting the ban reopens the shop (Priority: P1)

When the ban is lifted, the products come back exactly as they were - approved ones on sale, pending ones still
waiting - and anybody who saved one that is in stock is told it can be bought again (specs/075, 091).

**Why this priority**: A suspension that cannot be undone would be a deletion.

**Independent Test**: Ban, then lift: the product is on the shelf again, and its saver is told once.

**Acceptance Scenarios**:

1. **Given** a suspended seller, **When** the ban is lifted, **Then** Identity announces the shop reinstated.
2. **Given** that announcement, **Then** each of the seller's products is on the shelf again if it would be without the
   suspension, and each saver of one in stock is told.
3. **Given** the announcements arriving out of order, **Then** the later decision wins (the timestamp guard every read
   model here uses).

---

### US3 - A banned applicant is not given a shop (Priority: P2)

Approving a shop application for somebody who has since been banned is refused (409), so a banned person never
becomes a seller whose shop Catalog would open.

**Why this priority**: Closes the one path by which a ban and a new shop could cross.

**Independent Test**: Ban an applicant with a pending application; approving it is 409 and grants nothing.

**Acceptance Scenarios**:

1. **Given** a pending application whose applicant is banned, **When** staff approve it, **Then** 409 "The applicant is
   banned." and no role, profile or announcement.

---

### US4 - Staff can see that the shop is closed (Priority: P3)

On `/admin/users`, a banned seller's status reads "Banned · shop closed".

**Why this priority**: Drawing only; the server decides.

**Independent Test**: The users page renders a banned seller with the note, and a banned customer without it.

**Acceptance Scenarios**:

1. **Given** a banned account holding `Seller`, **Then** its status says the shop is closed.

### Edge Cases

- **A lock.** Does not close the shop (Decision). A locked seller's paid orders wait; staff may cancel them (specs/039).
- **Orders already paid to a suspended seller.** Unchanged: staff cancel them until a parcel ships (specs/039), which
  refunds the customer and restocks. Taking the parcel over is out of scope.
- **A seller Catalog has not heard of yet** (the suspension overtakes the registration): the sellers row is created with
  the suspension and an empty name that the registration fills in later - the name guard is separate from the
  suspension guard.
- **Bans from before this feature.** Catalog was never told; lifting and re-applying the ban announces it. No backfill
  (research D4).
- **Redelivery.** Guarded on the suspension's timestamp; a repeat changes nothing and tells nobody again.
- **A rollback.** An earlier image does not read the new column and would list a suspended seller's products again;
  expand-only, so nothing breaks.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Banning a user who holds `Seller` publishes `SellerSuspensionChangedEvent(SellerId, Suspended: true,
  ChangedAt)` through Identity's outbox, before the ban's one save; lifting the ban publishes it with `false`.
- **FR-002**: Catalog records the suspension on its `sellers` read model, guarded by `SuspensionChangedAt`, and copies it
  onto the seller's products (`products.SellerSuspended`) in the same transaction.
- **FR-003**: `Product.OnShelf` is `IsListed && IsActive && !SellerSuspended`; the listing's SQL spells out the same.
- **FR-004**: A reinstatement tells the savers of each of the seller's products that is back on sale and in stock, in the
  transaction that reinstates it (specs/091's helper).
- **FR-005**: Approving a banned applicant's shop application is 409 and changes nothing.
- **FR-006**: The users page shows "shop closed" for a banned seller (drawing only).
- **FR-007**: One migration adds `products.SellerSuspended` (default false) and `sellers.Suspended` /
  `SuspensionChangedAt` - expand only.

### Key Entities

- **Seller suspension** - on Catalog's `sellers` row (`Suspended`, `SuspensionChangedAt`), copied to each product
  (`SellerSuspended`) so "on the shelf" stays a property of the product.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A ban publishes one suspension announcement for a seller and none for a customer (Identity tests).
- **SC-002**: After Catalog records a suspension, the seller's product is absent from the listing, null to a shopper's
  lookup and unsellable to pricing; after a reinstatement it is back and its saver told once (Catalog tests).
- **SC-003**: Approving a banned applicant is 409 (Identity test).
- **SC-004**: Mutations - no announcement on ban; `OnShelf` ignoring the suspension; the timestamp guard removed - each
  turn a test red.
- **SC-005**: Bruno passes end to end: the ban in `admin-users/` and a product that disappears for shoppers.

## Decision

1. **A ban closes the shop; a lock does not.** A lock is a cooling-off - a moderator's is at most 30 days - and closing a
   shop for it would punish its buyers and its sales history for a rude review. A ban is indefinite and is what an
   administrator uses for fraud or counterfeits. Recorded as decided on the user's behalf ([research.md](research.md) D1).
2. **Eventually consistent, on purpose.** Specs/031 refused a read model for ownership because a stale answer there
   refuses the rightful owner. Here a stale answer sells a banned seller's product for a few more seconds, which staff
   can cancel (specs/039); the alternative - Catalog asking Identity on every listing and checkout - makes every shopper
   depend on Identity to see a page (D2).
3. **Copied onto products.** "On the shelf" stays a property of the product that every read, write and sale already asks
   (specs/092), rather than a join every caller must remember (D3).

## Assumptions

- Every seller holds the `Seller` role in Identity; the role, not the profile, decides "is a seller".
- Catalog's `sellers` table is keyed by the Identity user id (specs/027).

## Out of scope

- Closing a shop without banning the person (a moderator action) - a later issue if wanted.
- Staff taking over a suspended seller's parcels.
