# Data Model: A saved product back on sale by any route tells whoever saved it

**No table, column, index or migration changes** (FR-007). What changes is which transactions write the outbox rows
for `UserNotificationRequested` and `EmailRequested`.

## Tables read

| Table | Service | Read for |
| :-- | :-- | :-- |
| `saved_products` (`CustomerId`, `ProductId`, `SavedAt`) | Catalog | who to tell (`SaverIdsAsync`) |
| `products` (`ReviewStatus`, `IsActive`, `Availability`) | Catalog | is it on sale |
| `product_variants` (`IsActive`, `Availability`, `Price`) | Catalog | the rollup recompute |

## Tables written (unchanged shape)

- `products` - the rollup (`Price`, `Availability`, `AvailabilityObservedAt`, `UpdatedAt`) by the existing recompute
  statement; `ReviewStatus` by the existing guarded move.
- The outbox (`OutboxMessage`, `OutboxState`) - the notices and emails, now in the same transaction as the change on
  every route.

## "On sale" and its transitions

`on sale = ReviewStatus = Approved AND IsActive AND Availability`

```text
                       ┌─ Inventory: a variant has units again ────────┐
  not on sale ─────────┼─ a variant reactivated (rollup flips) ────────┼──▶ on sale  ⇒ tell each saver once
                       ├─ an edit whose recompute flips the rollup ────┤
                       └─ approved while active and in stock ──────────┘

  on sale ──any edit, price, announcement that leaves it on sale──▶ on sale   ⇒ tell nobody
```
