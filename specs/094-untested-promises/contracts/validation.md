# Contract: Behaviour promised but not held by a test

**Feature**: [spec.md](../spec.md)

One HTTP behaviour changes; no message or gRPC contract changes.

## `GET /api/reviews?hidden=&pageNumber=&pageSize=` (Catalog, `Admin` or `Moderator`) - `GetReviewsForStaffQuery`

| Query | Before | After |
| :-- | :-- | :-- |
| `pageNumber` < 1 | served (an empty or odd page) | **400**, `errors.PageNumber` |
| `pageSize` < 1 or > 50 | served - 100,000 in one response | **400**, `errors.PageSize` |
| `pageNumber` ≥ 1, `pageSize` 1-50 | 200 | 200 (unchanged) |

The same bounds as the public review list and the product review queue. The storefront's staff page asks for
`PAGE_SIZE` (12), inside the bounds.

## Messages the new tests read (unchanged)

- `SellerRegisteredEvent(SellerId, ShopName, OccurredAt)` - `ShopName` now asserted.
- `ReserveInventoryCommand(OrderId, Items[ProductId, Quantity, UnitPrice, VariantId])` and
  `ProcessPaymentCommand(OrderId, UserId, Amount, Currency)` - relayed fields asserted.
