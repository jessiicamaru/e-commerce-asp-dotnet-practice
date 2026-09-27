# Research: A seller pauses their shop, and staff close one

## D1 - Where the shop's state lives: Catalog, not Identity

**Decision**: add `PausedAt`, `ClosedAt`, `ClosedReason` and `ClosedBy` to Catalog's `sellers` row.

**Rationale**:

- The state decides one thing: whether the seller's products are on the shelf, and the shelf is Catalog's.
- Written where it takes effect, the pause and the products' flag commit in one transaction.
- A ban is different. It is about the *account*, which Identity owns, so it arrives as an event (specs/095).

**Alternatives rejected**:

- *Identity owns it and announces it, like the ban.* This adds an event and a consumer, and the seller's own pause
  would take effect seconds later for no reason. Identity would also hold a flag it never reads.
- *A column on each product only.* There would be no single place saying the shop is closed and why, and nowhere for
  the reason the seller reads.

## D2 - One shelf rule: widen the meaning of `products.SellerSuspended`

**Decision**: `products.SellerSuspended` becomes "the seller's shop is not open", written by one statement:

```sql
UPDATE products SET "SellerSuspended" = COALESCE((
    SELECT s."Suspended" OR s."PausedAt" IS NOT NULL OR s."ClosedAt" IS NOT NULL
      FROM sellers s WHERE s."SellerId" = @seller), false)
 WHERE "SellerId" = @seller
```

Every change of shop state calls this statement, including the ban path, which until now wrote the event's own bool.
`Product.OnShelf` and the three queries that spell it out are unchanged.

**Rationale**:

- The issue asks to reuse the shelf rule rather than add a second one.
- Recomputing from all three reasons means lifting one never reopens a shop that another keeps closed.
- An older image during a rollback still honours a pause or a closure, because it reads the same bool.

**Alternatives rejected**:

- *A second product column, `ShopClosed`.* Every query that spells `OnShelf` out would need to change. A rolled-back
  image would ignore the column and put a paused shop back on sale.
- *Joining `sellers` in `OnShelf`.* `OnShelf` is an in-memory property used across many handlers. A join would put a
  navigation on every product read.
- *Keeping the ban writing its own bool.* A ban lifted while the shop is paused would reopen the shop.

## D3 - A new product inherits the shop's state at approval

**Decision**: when `TryReviewAsync` moves a product to `Approved`, the same `UPDATE` sets `SellerSuspended` from the
`sellers` row.

**Rationale**:

- A seller's new product is `Pending`, so approval is the only way onto the shelf.
- A product created while its shop is paused would otherwise start with `false`, and go on sale when approved.
- Setting the flag at approval, in the approving statement, has no gap. A pause committing in the meantime updates the
  product after the approval's row lock is released.

**Alternative rejected**: reading the state when the product is created. That leaves a window where a pause and a
creation interleave, and it is not needed, because a `Pending` product is not on the shelf.

## D4 - Seller and staff are separate, and staff win

**Decision**:

- Pausing and reopening by the seller are guarded by `ClosedAt IS NULL`, and answer 409 "Staff closed this shop"
  otherwise.
- Staff closing is guarded by `ClosedAt IS NULL`; staff reopening by `ClosedAt IS NOT NULL`.
- Staff reopening leaves `PausedAt` as it is: the seller's own pause is theirs.

**Rationale**: The issue requires that a seller must not reopen a closed shop. Two columns make that a guard, not a
convention. Refusing the seller's pause as well keeps the seller's view simple: a closed shop has one thing to read,
the reason.

**Alternative rejected**: one state column (Open/Paused/Closed). Staff reopening would have to guess whether to return
the shop to Paused or to Open, and a rolled-back image could not parse a new value (specs/040's rule).

## D5 - Who closes: staff (Admin or Moderator)

**Decision**: `StaffRoles.Staff`.

**Rationale**: Closing a shop is moderation, like taking down a product (specs/045), which moderators do. It is
milder than a ban, which only an administrator may issue, and it can be undone by any staff member.

## D6 - Telling people

**Decision**:

- Staff closing or reopening notifies the seller: `ShopClosed {reason}` and `ShopReopened`.
- A shop back on the shelf, from any path, tells savers of in-stock products once, through `SavedProductNotices` as the
  ban's lifting does.
- No email.

**Rationale**: The seller sees the reason on `/shop` anyway. An email would add a template, and the shop being closed
is not urgent the way a lock is.

## D7 - The public shop page when paused

**Decision**:

- A paused shop's page answers with `paused: true` and a product count of 0.
- A closed or suspended shop is a 404, as a suspended one already is (specs/099).

**Rationale**: A shopper following a link to a shop on holiday should learn that it is on holiday, not that it
vanished. A closed shop is not a page.
