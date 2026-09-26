# Data Model: Saga payment timeout

> Written on 2026-09-27, after the feature merged (#135), from the code at that merge, the pull request and
> docs/architecture/saga-orchestration-roadmap.md.

**No table, column, index or migration changed.** The feature adds one reachable value to an existing text
column and reads rows that already exist.

## `order_state_data` (Orchestrator, `ecommerce_saga_db`, 5436)

The saga instance, persisted through `OrchestratorDbContext` with optimistic concurrency. Schema from
`20260902171030_InitialSagaSchema` and `20260922130744_AddSagaCurrency`:

| Column | Type | Role in this feature |
| :--- | :--- | :--- |
| `CorrelationId` | `uuid`, PK | The order id; what `PaymentTimeoutExpired` correlates on |
| `CurrentState` | `character varying(64)`, not null | Gains the value **`PaymentTimedOut`**. The sweeper filters on `InventoryReservedState` |
| `UpdatedAt` | `timestamp with time zone` | Set on entering `InventoryReservedState`; the start of the wait |
| `FailureReason` | `character varying(512)`, null | Set to "Payment did not answer in time." on timeout |
| `PaymentId` | `uuid`, null | Set by a late `PaymentProcessedEvent` before the refund is sent |
| `UserId`, `TotalAmount` (`numeric(18,2)`), `Currency` (`character varying(3)`), `CreatedAt` | | Unchanged |

No index was added for the sweep; the query is `WHERE "CurrentState" = 'InventoryReservedState' AND "UpdatedAt" <
@cutoff ORDER BY "UpdatedAt" LIMIT 200`.

The MassTransit inbox and outbox tables (`20260921104437_AddTransactionalOutbox`) carry what the sweeper and the
state machine publish.

## State transitions

```text
  OrderSubmitted ──▶ Submitted ──InventoryReserved──▶ InventoryReservedState
                         │                               │
                         └─ ReservationFailed ─▶ final   ├─ PaymentProcessed ─▶ final (OrderCompleted)
                                                         ├─ PaymentFailed ────▶ final (ReleaseInventory, OrderFailed)
                                                         │
                                                         └─ PaymentTimeoutExpired ─▶ PaymentTimedOut   (new)
                                                            (ReleaseInventory, OrderFailed)   │
                                                                                              ├─ PaymentProcessed ─▶ final (RefundPaymentCommand)
                                                                                              ├─ PaymentFailed ────▶ final (nothing)
                                                                                              └─ PaymentTimeoutExpired: ignored
```

| Transition | Published | Trigger |
| :--- | :--- | :--- |
| `InventoryReservedState` → `PaymentTimedOut` | `ReleaseInventoryCommand` ("Payment did not answer in time"), `OrderFailedEvent` | `PaymentTimeoutExpired` from the sweeper |
| `PaymentTimedOut` → final | `RefundPaymentCommand` | late `PaymentProcessedEvent` |
| `PaymentTimedOut` → final | nothing | late `PaymentFailedEvent` |
| `PaymentTimedOut` → `PaymentTimedOut` | nothing | a repeated `PaymentTimeoutExpired` (`Ignore`) |
| no instance | nothing, no log | `PaymentTimeoutExpired` after the order finished (`Discard`) |

`SetCompletedWhenFinalized()` removes a finalised instance's row, as before.

## `refunds` (Payment, `ecommerce_payment_db`, 5438) - unchanged

From `20260923170926_AddRefunds` (specs/039): `Id` (PK), `PaymentId` (FK → `payments`), `OrderId` (**unique**
`IX_refunds_OrderId` at this merge), `Amount` (`numeric(18,2)`, check `> 0`), `Currency`, `Provider`,
`RefundedAt`. A late approval's refund is one more row of the same kind; the unique `OrderId` makes it once-only.
The reason is not a column - it is written into the `RefundRecorded` audit entry's summary and snapshot.

## Rollback

`CurrentState` is text, so no schema changed and an older image starts. An older orchestrator does not know the
state `PaymentTimedOut`, so a late answer for an instance in it faults into the error queue rather than being
lost - recorded in the roadmap as a known limit.
