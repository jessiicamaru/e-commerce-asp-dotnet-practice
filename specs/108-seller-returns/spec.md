# Feature Specification: A seller's list of the returns of their parcels

**Feature Branch**: `108-seller-returns` | **Created**: 2026-09-27 | **Issue**: #215

**Status**: Draft

**Input**: Issue #215 - "a seller has no list of the returns of their parcels".

## Why

A seller learns of a return only from the `ReturnRequested` notice. The one list of returns is staff's
(`GET /api/orders/returns`, Admin). The shop console has products, sales, payouts, insights, vouchers and questions,
but no returns. `docs/features/returns.md` records "the sales list has no return badge" as a known limit. A seller who
misses a notice, or has several returns, has nowhere to see what is waiting on them.

## User Scenarios & Testing *(mandatory)*

### US1 - A seller sees the returns of their parcels (Priority: P1)

The shop console gains a Returns page. It lists the returns of the seller's own parcels one state at a time, oldest
waiting first, and opens on the ones asking for a decision. Each row leads to the sale, where the steps already are:
accept, refuse, received.

**Why this priority**: Returns wait on the seller, and a seller who cannot see them cannot act.

**Acceptance Scenarios**:

1. **Given** a return requested on a seller's parcel, **When** the seller opens their returns, **Then** it is listed
   under "Requested".
2. **Given** another seller's return, or a return of the shop's own parcel, **Then** it is never in this seller's list.
3. **Given** `?status=Nonsense`, **Then** 400.
4. **Given** a customer or an anonymous caller, **Then** 403 or 401.

---

### US2 - The sales list says which sales have a return (Priority: P2)

Each row of `/shop/sales` whose parcel has a return shows that return's state as a badge.

**Acceptance Scenarios**:

1. **Given** a sale whose parcel has a return, **Then** its row carries the return's state.
2. **Given** another seller's return on the same order, **Then** this seller's row carries nothing.

### Edge Cases

- **One order with parcels from several sellers**: each seller sees only their parcel's return. A return is per
  parcel, and `parcel_returns.SellerId` says whose.
- **The shop's own parcels** (`SellerId` null) are staff's and appear in no seller's list.
- **No amounts in the list.** The sale page shows the refund in its currency. This follows the staff page's rule
  (specs/067).
- **Paging**: the list is a queue, oldest waiting first (by `UpdatedAt`), like staff's.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `GET /api/orders/sales/returns?status=&page=&pageSize=` (Seller). It returns the returns whose
  `SellerId` is the caller's, optionally in one state, oldest waiting first, in the same `ReturnPage` shape as staff's.
- **FR-002**: `SaleSummaryResponse` gains `ReturnStatus`: the state of the return of the caller's parcel of that
  order, or null.
- **FR-003**: The storefront:
  - `/shop/returns` with one tab per state, opening on "Requested", each row linking to `/shop/sales/{orderId}`;
  - a menu entry;
  - the badge on `/shop/sales`.

## Success Criteria *(mandatory)*

- **SC-001**: A seller's list never contains another seller's or the shop's return. A test covers each.
- **SC-002**: Bruno reads the seller's return through the gateway.

## Assumptions

- The seller's steps stay on the sale page. The list only leads there.
- There is no count of waiting returns in the menu. The first tab is the waiting ones.
