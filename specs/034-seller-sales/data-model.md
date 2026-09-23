# Data model: A seller can see what they sold

## `order_items` gains one column (Order database)

| Column | Type | Null | Meaning |
| :-- | :-- | :-- | :-- |
| `SellerId` | `uuid` | yes | Whose product this line was, **as Catalog said at checkout**. Null: the shop's own, a line from before this feature, or a Catalog that could not say (logged). |

Index `IX_order_items_SellerId`.

Written once, at submission, and never updated - the same life as `UnitPrice` and `ProductName`.

**Migration `AddOrderItemSeller`**: add a nullable column and an index. Expand-only: an earlier
Order image ignores the column, so rolling back does not strand anything, and the
`schema-compatibility` job has nothing to flag.

No backfill (research D7).

## No change to Catalog's data

`products.SellerId` already exists (specs/027). Catalog only starts **saying** it in the pricing
answer.

## What reads it

| Read | Filter |
| :-- | :-- |
| sales list | `EXISTS (item WHERE SellerId = @me) AND Status IN (Paid, Completed, Preparing, Shipped)` |
| one sale | the same, plus `Id = @orderId`; only `@me`'s items loaded |
