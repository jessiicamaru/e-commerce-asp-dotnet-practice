# Phase 1 Data Model: Ratings and reviews

> Written on 2026-09-27, after the feature merged (#98), from the code at that merge, the pull request and docs/features/ratings-and-reviews.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

Everything new is in Catalog's `ecommerce_catalog_db`: two tables and two columns on `products`, all in one
migration, `20260923212320_AddProductReviews`. Order's schema did not change - the feature reads
`order_shipments` and `order_items` as they were and only changes how the sweep selects its rows (below).
The mapping is `ReviewConfiguration`, `ReviewEligibilityConfiguration` and `ProductConfiguration` in
`Ecommerce.Catalog.Infrastructure/Configurations/`.

---

## `product_reviews` (Catalog)

One row per customer per product. Entity `Review`.

| Column | Type | Constraints | Meaning |
| :-- | :-- | :-- | :-- |
| `Id` | uuid | PK (`PK_product_reviews`), `ValueGeneratedNever` | `Guid.CreateVersion7()` in the entity |
| `ProductId` | uuid | Not null, FK → `products.Id` `ON DELETE CASCADE` | The product reviewed |
| `CustomerId` | uuid | Not null | The author - the token's subject |
| `AuthorName` | varchar(100) | Not null | The token's `given_name` when first written, or the email's first letter and a dot |
| `Rating` | integer | Not null, CHECK `CK_product_reviews_rating`: `"Rating" BETWEEN 1 AND 5` | Stars |
| `Body` | varchar(2000) | Nullable | The words; blank is stored as null, text is trimmed |
| `CreatedAt` | timestamp with time zone | Not null | Written |
| `UpdatedAt` | timestamp with time zone | Not null | Last edited; `edited` in responses is `UpdatedAt > CreatedAt + 1 s` |
| `HiddenAt` | timestamp with time zone | Nullable | Set when a moderator hides it; null means visible |
| `HiddenReason` | varchar(500) | Nullable | Required by the validator when hiding |
| `HiddenBy` | uuid | Nullable | The staff member who hid it |

**Indexes**:

- `IX_product_reviews_ProductId_CustomerId` - **unique**. FR-002: one review per customer per product, enforced
  by the database; a second write is an edit (research D8).
- `IX_product_reviews_ProductId_CreatedAt` - the public list, a product's reviews newest first.

**Invariants**: the rating CHECK duplicates the validator's rule (`InclusiveBetween(1, 5)`) so a path that skips
validation cannot store 0 or 6. Deleting a product deletes its reviews (cascade).

---

## `review_eligibility` (Catalog)

Who received which product. Entity `ReviewEligibility`. Fed only by `ReviewEligibilityConsumer`.

| Column | Type | Constraints | Meaning |
| :-- | :-- | :-- | :-- |
| `ProductId` | uuid | PK part 1 | A product the customer received |
| `CustomerId` | uuid | PK part 2 | The buyer - `ParcelDeliveredEvent.BuyerId` |
| `FirstDeliveredAt` | timestamp with time zone | Not null | The delivery that first granted it |

**Primary key**: `PK_review_eligibility` on (`ProductId`, `CustomerId`). Written with
`INSERT ... ON CONFLICT ("ProductId", "CustomerId") DO NOTHING`, one statement per distinct product in the event,
so a redelivered event or the same product in a second parcel changes nothing and `FirstDeliveredAt` keeps the
first delivery.

**No foreign key** to `products` (whether that was deliberate is not recorded): a row can name a product later deleted.
It is harmless - the write handler looks the product up first and answers 404 - and is listed in the feature's
known limits.

---

## `products` (Catalog) - two columns added

| Column | Type | Constraints | Meaning |
| :-- | :-- | :-- | :-- |
| `RatingAverage` | numeric(3,2) | Nullable | Average of the visible reviews, rounded to 2 places; null when there are none |
| `RatingCount` | integer | Not null, default 0 | How many visible reviews |

Both are written only by `ReviewRepository.SaveAndRecomputeAsync`:

```sql
UPDATE products SET
    "RatingCount"   = (SELECT count(*) FROM product_reviews WHERE "ProductId" = @id AND "HiddenAt" IS NULL),
    "RatingAverage" = (SELECT round(avg("Rating")::numeric, 2) FROM product_reviews WHERE "ProductId" = @id AND "HiddenAt" IS NULL)
WHERE "Id" = @id
```

run in the same transaction as the `SaveChangesAsync` that writes the review, its audit entry and its notice
(outbox rows), inside the context's execution strategy. Recomputed, never incremented (research D3).

---

## Order: what is read, and the sweep's new shape

No table or column changed in `ecommerce_order_db`.

- **The customer's confirmation** (`TryConfirmDeliveryAsync`, specs/040) was already one guarded `UPDATE
  order_shipments SET "DeliveredAt", "DeliveryConfirmedBy" = 'Customer' WHERE ... "Status" = 'Shipped' AND
  "DeliveredAt" IS NULL` in its own transaction, calling `stage` only when it changed the row. The stage now also
  announces the parcel.
- **The sweep** (`AutoConfirmDeliveriesAsync` → `SweepDeliveriesAsync`) changed from one `UPDATE` returning a
  count to:
  1. `SELECT * FROM order_shipments WHERE "Status" = 'Shipped' AND "DeliveredAt" IS NULL AND "ShippedAt" IS NOT
     NULL AND "ShippedAt" <= @shippedBefore FOR UPDATE SKIP LOCKED` - the ids only;
  2. the guarded `UPDATE` restricted to those ids, setting `DeliveredAt` and `DeliveryConfirmedBy = 'Auto'`;
  3. the ids passed to `stage` (whose signature changed from `Func<int, ...>` to
     `Func<IReadOnlyList<Guid>, ...>`), then `SaveChangesAsync` and commit.
- **A parcel's products** (`GetDeliveredParcelsAsync`): `order_shipments` with `DeliveredAt` set, joined to
  `orders` for `UserId`, and each `order_items` row whose `SellerId` equals the shipment's `SellerId` (null
  matching null for the shop's own part), grouped per shipment with distinct `ProductId`s. Read inside the
  delivery's transaction, so it sees the `DeliveredAt` just written.

---

## State of a review

```text
            PUT .../reviews/mine (eligible)
   none ─────────────────────────────────▶ Visible ◀──────────┐
                                             │   ▲             │ PUT (edit: Rating, Body, UpdatedAt)
                                             │   └─────────────┘
                           POST .../hide     │   ▲  POST .../restore
                           (reason)          ▼   │
                                            Hidden
```

| Transition | Columns | Guard at this merge | Recompute |
| :-- | :-- | :-- | :-- |
| none → Visible | insert | eligibility row exists; unique (`ProductId`, `CustomerId`) | yes |
| Visible → Visible (edit) | `Rating`, `Body`, `UpdatedAt` | the caller's own row | yes |
| Visible → Hidden | `HiddenAt`, `HiddenReason`, `HiddenBy` | `HiddenAt` read as null, else 409 | yes |
| Hidden → Visible | the three cleared | `HiddenAt` read as not null, else 409 | yes |

An author may edit a hidden review; it stays hidden. At this merge the hide and restore guards are a read then a
tracked save, not a single guarded statement - the reason two concurrent hides could both succeed. Specs/057
made both a guarded `UPDATE ... WHERE "HiddenAt" IS [NOT] NULL`.

---

## Schema compatibility

Purely additive: two new tables, a nullable column and a `NOT NULL` column **with a default**. The previous
Catalog image runs against the new schema unchanged (constitution, Schema evolution). The `Down` migration drops
both tables and both columns.
