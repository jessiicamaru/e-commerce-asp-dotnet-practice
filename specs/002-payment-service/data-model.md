# Phase 1 Data Model: Payment Service

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One table in `ecommerce_payment_db`, plus the MassTransit inbox and outbox tables added by
`AddTransactionalOutboxEntities()`.

---

## `payments`

One row per order. Not per attempt: a redelivered request resolves to the row already there.

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `Id` | uuid | PK | Identity of the payment, and the `PaymentId` sent back to the saga |
| `OrderId` | uuid | **Unique**, not null | The order being paid for |
| `UserId` | uuid | Not null | Who it was for, as supplied by the request |
| `Amount` | numeric(18,2) | Not null | As supplied; never recalculated (research D4) |
| `Status` | varchar(16) | Not null | `Approved` or `Rejected` |
| `FailureReason` | varchar(512) | Null unless rejected | Why it was refused |
| `Provider` | varchar(32) | Not null, default `Stub` | What produced the outcome |
| `ProcessedAt` | timestamptz | Not null | When the outcome was decided |

**Constraints**:

- Unique on `OrderId` — FR-005 and FR-006. This is the guarantee, not an optimisation: it is what
  stops two simultaneous requests from both inserting.
- Check `Amount > 0` only holds for approved rows, so it is **not** a table constraint: a rejection
  for a non-positive amount must still be recorded, with the offending amount visible. The
  validation lives in the handler, and the record of what was refused is the point.
- `Provider` defaults to `Stub` and is written explicitly — research D3. A row that does not say
  where its outcome came from is a row somebody will later assume came from a bank.

**Indexes**: unique on `OrderId` (also the lookup path for the payment query).

---

## States

```text
                 ┌────────────┐
   request ─────▶│  Approved  │   the configured outcome is Approve and the amount is valid
                 └────────────┘
                 ┌────────────┐
   request ─────▶│  Rejected  │   the amount is not positive, or the configured outcome is Reject
                 └────────────┘
```

There is no transition between them. A payment is decided once, when it is created, and a later
request for the same order reads the existing row rather than changing it. That is what makes
replay safe without a guarded update: the row is immutable after insert.

| Outcome | Reply published | Effect downstream |
| :--- | :--- | :--- |
| `Approved` | `PaymentProcessedEvent` | Saga publishes `OrderCompletedEvent`; Inventory confirms and deducts stock permanently |
| `Rejected` | `PaymentFailedEvent` | Saga publishes `ReleaseInventoryCommand`; Inventory returns the held units |

---

## Transaction rules

1. One request is one transaction: read any existing payment, insert if absent, publish the reply,
   then `SaveChangesAsync` once. The row and the outbox entry commit together.
2. If the insert loses a race, the unique violation is caught, the winning row is re-read, and the
   reply reports **that** row's outcome. Dropping the message instead would leave the saga waiting
   forever; replying independently could contradict what was recorded.
3. A request that resolves to an existing payment still publishes a reply. The saga may have missed
   the first one, and a reply it has already handled is discarded by correlation.
