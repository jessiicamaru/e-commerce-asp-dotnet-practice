# Data Model: Somewhere for the Order to Go

Two services change: Identity gains an address book, Order gains a destination, a delivery choice
and three statuses. No message contract changes.

---

## Identity — `delivery_addresses` (new table)

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | `uuid` PK | `Guid.CreateVersion7()`, mapped `ValueGeneratedNever()` |
| `UserId` | `uuid` NOT NULL | FK → `users.Id`, `ON DELETE CASCADE`; indexed |
| `RecipientName` | `varchar(100)` NOT NULL | |
| `Line1` | `varchar(200)` NOT NULL | |
| `Line2` | `varchar(200)` NULL | |
| `City` | `varchar(100)` NOT NULL | |
| `Region` | `varchar(100)` NULL | state / province |
| `PostalCode` | `varchar(16)` NOT NULL | shape-checked only |
| `Country` | `char(2)` NOT NULL | ISO 3166-1 alpha-2, upper-cased on save |
| `Phone` | `varchar(30)` NULL | |
| `IsDefault` | `boolean` NOT NULL | |
| `CreatedAt`, `UpdatedAt` | `timestamptz` NOT NULL | |

**Constraints**

- `UNIQUE ("UserId") WHERE "IsDefault"` — at most one default per customer (research D10).
- Every write for a user takes `SELECT 1 FROM users WHERE "Id" = @me FOR UPDATE` first, so
  "demote old default, promote new one" and the 20-address limit cannot race.

**Invariants** (code, backed by the index and the lock)

- A customer with ≥ 1 address has **exactly one** default.
- The first address saved is the default.
- Deleting the default promotes the most recently created remaining address.
- At most 20 addresses per customer.

---

## Order — `orders` (new columns, all nullable)

| Column | Type | Notes |
| :--- | :--- | :--- |
| `ShipTo_RecipientName` | `varchar(100)` | owned type `ShippingAddress` |
| `ShipTo_Line1` / `ShipTo_Line2` | `varchar(200)` | |
| `ShipTo_City` / `ShipTo_Region` | `varchar(100)` | |
| `ShipTo_PostalCode` | `varchar(16)` | |
| `ShipTo_Country` | `varchar(2)` | |
| `ShipTo_Phone` | `varchar(30)` | |
| `ShippingOptionCode` | `varchar(32)` | e.g. `express` |
| `ShippingOptionName` | `varchar(100)` | frozen display name |
| `ShippingPrice` | `decimal(18,2)` | frozen price |
| `TrackingReference` | `varchar(100)` | set on *Shipped* |

All nullable: orders placed before this feature have none of them, and show "no destination
recorded" rather than an invented one. **Additive** under the constitution's schema rule — an image
from before this feature ignores the new columns.

`TotalAmount` = Σ line totals + `ShippingPrice`. It is still the number `OrderSubmittedEvent`
carries, so the saga charges it without any contract change.

**No link to the address book.** There is no `AddressId` column on purpose: an order holds a copy,
and nothing can join it back to an address that may since have been edited or deleted.

---

## Order — `OrderStatus`

```text
Pending(1)  Submitted(2)  StockReserved(3)  Paid(4)  Completed(5)  Cancelled(6)  Failed(7)
Preparing(8)  Shipped(9)                                     ← new
```

Stored as strings; adding values is additive.

| From | Trigger | To | Guard |
| :--- | :--- | :--- | :--- |
| — | checkout | `Submitted` | |
| `Submitted` | `OrderCompletedEvent` | **`Paid`** (was `Completed`) | `WHERE "Status" = 'Submitted'` |
| `Submitted` | `OrderFailedEvent` | `Failed` | `WHERE "Status" = 'Submitted'` |
| `Paid` | Admin: prepare | `Preparing` | `WHERE "Status" = 'Paid'` |
| `Preparing` | Admin: ship + tracking | `Shipped` | `WHERE "Status" = 'Preparing'` |

**Reachability after this feature**

| Value | Reachable | Note |
| :--- | :--- | :--- |
| `Pending` | No | unchanged |
| `Submitted` | Yes | |
| `StockReserved` | No | unchanged — the saga still announces no reservation |
| `Paid` | **Yes** | **changed** — was unreachable by design in specs/003 |
| `Completed` | **No longer written** | kept so old rows and rolled-back images parse; read as `Paid` |
| `Cancelled` | No | unchanged — nothing cancels an order |
| `Failed` | Yes | |
| `Preparing`, `Shipped` | Yes | new |

**Data migration**: `UPDATE orders SET "Status" = 'Paid' WHERE "Status" = 'Completed'` — safe in
both directions (research D3).

---

## Order — delivery options (configuration, not a table)

```json
"Shipping": {
  "Options": [
    { "Code": "standard", "Name": "Standard delivery", "Price": 5.00 },
    { "Code": "express",  "Name": "Express delivery",  "Price": 15.00 }
  ]
}
```

Codes are matched case-insensitively. At startup Order refuses to run with fewer than one option,
a duplicate code, or a negative price — a misconfiguration fails at startup, not at checkout.
