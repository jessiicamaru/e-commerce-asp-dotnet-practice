# Data Model: Admin revenue less returns

> Written on 2026-09-27, after the feature merged (#176), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration changed.** The feature reads existing Order tables differently.

---

## What is read

| Table | Columns | Why |
| :--- | :--- | :--- |
| `orders` | `Id`, `UserId`, `Status`, `PaidAt`, `CreatedAt`, `Currency`, `TotalAmount` | The sold orders of the period (unchanged) |
| `order_items` | `ProductId`, `SellerId`, `Quantity`, `UnitPrice`, `ProductName` | Top products (unchanged), now filtered |
| `order_shipments` | `OrderId`, `SellerId` | Which parcel a line belongs to (specs/035) |
| `parcel_returns` | `OrderId`, `ShipmentId`, `Status`, `RefundAmount` (`numeric(18,2)`, nullable) | Which parcels came back, and for how much (specs/066) |

## The rules, as the queries write them

```text
Sold(o)            = o.Status IN (Paid, Completed, Preparing, Shipped)
                     AND from <= (o.PaidAt ?? o.CreatedAt) < to
ReturnedIn(from,to) = for each Sold order o, each parcel_returns r
                     with r.OrderId = o.Id AND r.Status = 'Received':
                       (o.UserId, o.PaidAt, o.CreatedAt, o.Currency, r.RefundAmount ?? 0)

Revenue(day, cur)  = Σ o.TotalAmount   over Sold on that shop day in cur
                   - Σ refund          over ReturnedIn on that shop day in cur
Orders(day, cur)   = count of Sold     (unchanged: the order is still one order)
Spent(buyer, cur)  = Σ o.TotalAmount - Σ refund, per (buyer, currency)
TopProducts        = lines of Sold orders, except a line whose order has a parcel
                     with the line's SellerId (null = the shop's) and a Received return
```

The shop day is `(PaidAt ?? CreatedAt) AT TIME ZONE '<Insights:TimeZone>'` (specs/082).

## Return states and what they do here

| `parcel_returns.Status` | Effect on the Overview |
| :--- | :--- |
| `Requested`, `Accepted`, `Refused`, `Escalated`, `Rejected`, `SentBack` | None - the parcel is still revenue |
| `Received` | Its `RefundAmount` leaves revenue and spending; its lines leave top products |

## Schema evolution

Nothing to evolve. An earlier Order image reports gross revenue again; it does not fail.
