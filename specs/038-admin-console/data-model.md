# Data Model: An administrator's console

> Written on 2026-09-27, after the feature merged (#82, #83), from the code at that merge, the pull
> requests and docs/features/fulfilment-and-delivery.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

**No table, column, index or migration changed.** The feature adds one read and a set of pages; every
write it offers goes through an endpoint that existed before it (specs/035 for the shop's parcel,
specs/037 for payouts).

## What the new read reads

`GetOrderForStaffQuery` loads one order through `IOrderRepository.GetByIdAsync` - the order, its
`order_items` and its `order_shipments` - and maps it with `OrderMapping.ToDetail`, the same mapping the
owner's read uses. It differs from the owner's read only in having no `UserId` filter.

## What the console derives

| Shown | From |
| :-- | :-- |
| the shop's parcel | the `shipments[]` entry with `isShop: true`; with no parts at all, the whole order in its own status (`shopParcelOf`) |
| the next step | the shop's parcel status: `Paid` → prepare, `Preparing` → ship, `Shipped` → none |
| the queue per state | `GET /api/orders/fulfilment?status=` (by the shop's part, specs/035) |
| payouts due | `GET /api/orders/payouts/due` - `sellerId`, `sellerName`, `currency`, `due`, `parts` |

## States

None new. The shop's parcel moves `Pending` (read as `Paid`) → `Preparing` → `Shipped` exactly as
specs/035 defined; the console only offers the next one.
