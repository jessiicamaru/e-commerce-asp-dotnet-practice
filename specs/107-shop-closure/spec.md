# Feature Specification: A seller pauses their shop, and staff close one

**Feature Branch**: `107-shop-closure` | **Created**: 2026-09-27 | **Issue**: #214

**Status**: Draft

**Input**: Issue #214 - "a shop can be closed only by banning its seller, and a seller cannot pause it".

## Why

Since specs/095 the only way a shop leaves the shelf is a ban of its seller (`sellers.Suspended`, copied onto
`products.SellerSuspended`). Two things are missing:

- **A seller going on holiday cannot pause.** Orders keep arriving that nobody will ship for two weeks.
- **Staff who want a shop gone, but the person kept as a customer, have only the ban.** A ban ends every session and
  stops the person buying too.

`grep -ri "Vacation|PauseShop|CloseShop" server/src` finds nothing.

## User Scenarios & Testing *(mandatory)*

### US1 - A seller pauses and reopens their shop (Priority: P1)

A seller about to be away presses "Pause shop" on `/shop`. Their products leave the shelf everywhere: the listing, the
product page, checkout. The shop's page says it is taking a break. Orders already paid still wait for the seller to
ship them. Coming back, they press "Reopen". Everything they had on sale is back, and whoever saved one of those
products is told it is available again.

**Why this priority**: It is the everyday case, and the one sellers are hurt by today.

**Acceptance Scenarios**:

1. **Given** an open shop with a product on sale, **When** its seller pauses, **Then** the product is not listed, its
   page is a 404 to a shopper, and checkout refuses it.
2. **Given** a paused shop, **When** a shopper opens the shop's page, **Then** it answers and says the shop is paused,
   with no products.
3. **Given** a paused shop, **When** its seller reopens it, **Then** the product is back on sale, and a shopper who
   saved it is told once.
4. **Given** a paused shop, **When** its seller pauses again, **Then** 409. **Given** an open shop, reopening is 409.
5. **Given** a paid order with a part from the paused shop, **Then** the seller can still prepare and ship it.

---

### US2 - Staff close a shop without touching the account (Priority: P1)

A moderator or administrator closes a shop with a reason. The seller reads the reason on `/shop` and in a notice, and
can still sign in, buy, and ship what they have already sold. Only staff reopen it; the seller cannot. Staff find the
closed shops in a tab of `/admin/shops`.

**Why this priority**: Without it, the only way to act on a bad shop is to ban the person.

**Acceptance Scenarios**:

1. **Given** an open shop, **When** staff close it with a reason, **Then** its products leave the shelf, the shop's
   page is a 404 to a shopper, and the seller is notified with the reason.
2. **Given** a closed shop, **When** its seller pauses or reopens it, **Then** 409 saying staff closed it.
3. **Given** a closed shop, **When** staff reopen it, **Then** the products are back on sale (unless the seller had also
   paused, or is banned), the seller is told, and savers are told.
4. **Given** a closed shop, **When** staff close it again, **Then** 409.
5. **Given** a customer or a seller, **When** they try to close a shop, **Then** 403.
6. **Given** no reason, **Then** 400.

### Edge Cases

- **Banned, paused and closed are three independent reasons.** The shelf asks "is any of them true". Lifting one of
  them (for example, a ban lifted while the seller has paused) must not reopen the shop.
- **A product approved while its shop is paused or closed** stays off the shelf. Approval is the one way a new product
  reaches the shelf, so approval takes the shop's state with it.
- **An unknown seller, or one whose name has not arrived from Identity yet**: 404 "Shop not found." for every route.
- **The shop's own products** (`SellerId` null) are never paused or closed by this.
- **Paid orders** are untouched: the seller still ships them. Staff can cancel them (specs/039, 104).
- **A cart holding a paused shop's product** sees it unavailable, and checkout refuses it, as for any product off the
  shelf.
- **Two staff closing at once**: one wins, the other gets 409 (guarded `UPDATE`).
- **A rolled-back Catalog image** reads the products' flag as it is, so a paused shop stays paused. Only if a ban is
  lifted during the rollback would the older image reopen it. That is recorded, and accepted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Catalog's `sellers` row gains `PausedAt`, `ClosedAt`, `ClosedReason` and `ClosedBy`. A shop is open
  when it is not suspended, not paused and not closed.
- **FR-002**: `products.SellerSuspended` means "the seller's shop is not open", and one statement writes it from the
  `sellers` row. Every change calls that statement: suspension, pause, reopen, close, staff reopen, and approval of a
  product. `Product.OnShelf` is unchanged.
- **FR-003**: `GET /api/shops/mine` (Seller) returns the shop's state and, when closed, the reason.
  `POST /api/shops/mine/pause` and `/reopen` (Seller) each run one guarded `UPDATE`.
- **FR-004**: `POST /api/shops/{sellerId}/close` with a reason, and `POST /api/shops/{sellerId}/reopen` (Staff), each run
  one guarded `UPDATE`. `GET /api/shops/closed` (Staff, paged) lists closed shops.
- **FR-005**: `GET /api/shops/{sellerId}` (anonymous) answers a paused shop with `paused: true`, and is 404 for a closed
  or suspended one.
- **FR-006**: Every change is audited: pause and reopen under Catalog, close and staff reopen under Moderation. Staff
  closing or reopening a shop notifies the seller (`ShopClosed` with the reason, `ShopReopened`). A shop coming back on
  the shelf tells savers (`SavedBackInStock`, specs/091). All of it is staged in the change's own transaction.
- **FR-007**: The storefront:
  - a shop card on `/shop` with Pause or Reopen, or, when closed, the reason;
  - a paused banner on `/shops/:id`, with a staff "Close shop" dialog there;
  - a "Closed shops" tab on `/admin/shops` with Reopen.

## Success Criteria *(mandatory)*

- **SC-001**: A paused or closed shop sells nothing, which checkout's pricing path shows as `Sellable = false`.
- **SC-002**: No change of shop state reopens a shop that another reason keeps closed. A test covers each pair.
- **SC-003**: A seller cannot reopen a shop staff closed, whatever they send.
- **SC-004**: Bruno covers the round trip and its 403/409 cases through the gateway.

## Assumptions

- A pause has no end date: the seller reopens by hand.
- A closure is not an email, only a notice and an audit entry. The seller also sees the reason on `/shop`.
- One closure reason at a time. Its history is the audit log.
- No new status value anywhere: the new state is columns.
