# Data Model: Cancelling a paid order

> Written on 2026-09-27, after the feature merged (#84), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

Two additive migrations in two services, and a new use of an existing state in a third. **No enum gained
a value in any service** (research D3).

---

## Order — `orders.CancelledBy`

Migration `20260923165912_AddOrderCancelledBy`.

| Column | Type | Null | Meaning |
| :-- | :-- | :-- | :-- |
| `CancelledBy` | `character varying(16)` | yes | `Customer` or `Staff` (`Cancellation.ByCustomer` / `ByStaff`); null for every order not cancelled, and for any cancelled before this |

`orders.Status` reaches `Cancelled`, which has been in `OrderStatus` since feature 003 - an older image
parses it.

### Order state transition

```text
Paid / Completed / Preparing ──cancel──▶ Cancelled
```

| From | Who may | Refused with |
| :-- | :-- | :-- |
| `Paid` or `Completed`, every part `Pending` | customer (own) or staff | - |
| any part `Preparing`, none `Shipped` | staff only | customer: 409 `This order is being prepared; ask the shop to cancel it.` |
| any part `Shipped` | nobody | 409 `Part of this order has been shipped; it can no longer be cancelled.` |
| `Submitted`, `Failed`, `Pending`, `StockReserved` | nobody | 409 `Only a paid order can be cancelled.` |
| `Cancelled` | - | nothing: 200, no second event |

The write, in `OrderRepository.TryCancelAsync`, inside `CreateExecutionStrategy()` and one transaction:
lock the order row (`SELECT 1 FROM orders WHERE "Id" = @id FOR UPDATE`), read the status (with the owner
in the `WHERE` for a customer), create missing parts in the order's state, read the parts, decide, then

```sql
UPDATE orders SET "Status" = 'Cancelled', "CancelledBy" = @by, "UpdatedAt" = @at
WHERE "Id" = @id AND "Status" IN ('Paid','Completed','Preparing','Shipped')
```

(EF `ExecuteUpdateAsync`), stage `OrderCancelledEvent` into the outbox, save once, commit. The parts are
left in whatever state they were; a cancelled order's parts still exist with their terms.

### Money

`Sales.Statuses` (what a seller sees) = Paid, Completed, Preparing, Shipped, **Cancelled**.
`Sales.Earning` (what balances, the due list and the payout claim count) = Paid, Completed, Preparing,
Shipped. `PayoutRepository` switched from the first to the second - the only thing keeping a cancelled
order's parts out of a balance.

## Payment — `refunds` (new)

Migration `20260923170926_AddRefunds`.

| Column | Type | Null | Meaning |
| :-- | :-- | :-- | :-- |
| `Id` | `uuid` | no | PK `PK_refunds`, application-generated (`ValueGeneratedNever`, v7) |
| `PaymentId` | `uuid` | no | FK `FK_refunds_payments_PaymentId` → `payments.Id`, `ON DELETE RESTRICT`; index `IX_refunds_PaymentId` |
| `OrderId` | `uuid` | no | **unique** `IX_refunds_OrderId` - one refund per order, however often the event arrives |
| `Amount` | `numeric(18,2)` | no | the payment's amount; `CK_refunds_amount_positive` (`"Amount" > 0`) |
| `Currency` | `character varying(3)` | yes | the payment's currency |
| `Provider` | `character varying(32)` | no | the payment's provider - `Stub` today, so no money moved |
| `RefundedAt` | `timestamp with time zone` | no | |

`payments` is untouched: a payment row stays immutable, and a `PaymentStatus` value such as "Refunded"
would be one an older image could not parse. A refund is written only for an **approved** payment.

(Later, specs/066 added `refunds.ReturnId` and made the `OrderId` index partial for parcel returns; that
is not part of this feature.)

## Inventory — reservation transitions, no migration

`stock_reservations` and `stock_items` are unchanged. `RestockCancelledOrderCommandHandler`, under
`FOR UPDATE` on the stock rows in ascending product order:

| Reservation from | Effect on the stock item | Reservation to | `SettlementReason` |
| :-- | :-- | :-- | :-- |
| `Confirmed` (completion arrived first) | `QuantityOnHand += Quantity` | `Released` | `Returned: order cancelled` |
| `Held` (cancellation arrived first) | `QuantityReserved -= Quantity` | `Released` | `Returned: order cancelled` |

The guard is the status each row moves **from**: a redelivery finds neither and moves nothing, and a
late `OrderCompletedEvent` finds no `Held` row and deducts nothing. The handler announces availability -
the seventh path that moves stock.

## What an older image sees

Order: `Cancelled` rows it can parse, a column it ignores. Payment: a table it ignores. Inventory: `Released`
rows, which it already knew. An older Order image does not publish `OrderCancelledEvent`, and has no
route to cancel.
