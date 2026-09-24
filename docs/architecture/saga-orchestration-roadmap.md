# Saga Orchestration Pattern & System Architecture Roadmap

This document outlines the architectural concept of the **Saga Pattern**, compares **Orchestration vs. Choreography**, and records the phased roadmap that built our distributed E-commerce Monorepo Microservices system.

---

## 1. Deep Dive: What is the Saga Pattern?

### 1.1 The Challenge of Distributed Transactions
In a traditional monolithic application with a single relational database, maintaining data consistency across multiple tables relies on **ACID Transactions** (`BEGIN TRANSACTION ... COMMIT / ROLLBACK`).

In a **Database-per-Service Microservices Architecture**, each service owns an isolated PostgreSQL database (`ecommerce_identity_db`, `ecommerce_catalog_db`, `ecommerce_order_db`, etc.). Standard ACID transactions cannot span across separate database networks. 

Using traditional Distributed 2-Phase Commit (2PC) protocols is strongly discouraged in microservices because 2PC creates tight runtime coupling, blocking locks, and high latency.

```text
Monolith (ACID Transaction):
[Order Table + Inventory Table + Payment Table] ──► Single DB Commit / Rollback (Instant)

Microservices (Distributed System):
[Order DB] ──(Network)──► [Inventory DB] ──(Network)──► [Payment DB]
   ▲                             ▲                            ▲
   │                             │                            │
Local Tx 1                   Local Tx 2                   Local Tx 3
```

---

### 1.2 The Saga Pattern Definition
The **Saga Pattern** solves distributed data consistency by breaking a global transaction into a sequence of **Local Transactions** ($T_1, T_2, \dots, T_n$):

1. Each local transaction updates the database of a single microservice and emits a message/event.
2. The next service receives the message and executes its own local transaction.
3. **Compensating Transactions ($C_1, C_2, \dots, C_{n-1}$)**: If any local transaction $T_k$ fails (e.g., credit card payment declined), the Saga executes compensating transactions in reverse order ($C_{k-1}, \dots, C_1$) to undo the changes made by previous steps.

```text
Successful Saga Execution:
[T1: Create Order] ──► [T2: Reserve Stock] ──► [T3: Charge Card] ──► [Saga Completed]

Failed Saga with Compensation (Rollback):
[T1: Create Order] ──► [T2: Reserve Stock] ──► [T3: Charge Card FAILS!]
                                                      │
                       ┌──────────────────────────────┘
                       ▼
            [C2: Release Stock] ──► [C1: Cancel Order] ──► [Saga Aborted]
```

---

### 1.3 Saga Approaches: Orchestration vs. Choreography

| Feature | Choreography (Event-Driven) | **Orchestration (Centralized State Machine - Chosen)** |
| :--- | :--- | :--- |
| **Control Mechanism** | Decentralized; services react to events directly. | Centralized; a dedicated **Saga Orchestrator Service** controls the flow. |
| **Coupling** | Services must know about events from other services. | Services only execute commands sent by the Orchestrator. |
| **Visibility & Debugging** | Hard to trace global transaction state. | **Easy to monitor** via Orchestrator State Machine database. |
| **Cyclic Dependencies** | Risk of circular event dependencies. | No circular dependencies. |
| **Best For** | Simple workflows (2-3 steps). | **Complex distributed workflows** (Checkout, Refunds, Shipping). |

---

## 2. Standalone Saga Orchestrator Microservice Architecture

We adopt **Saga Orchestration** by building a dedicated, standalone microservice: **`Ecommerce.Orchestrator`**.

```mermaid
graph TD
    Client["Client"] --> Gateway["API Gateway - Port 5000"]
    Gateway --> OrderService["Order Service - Port 5059"]

    OrderService -->|1. OrderSubmittedEvent| RabbitMQ["RabbitMQ Broker - Port 5672"]
    RabbitMQ -->|2. Event received| Orchestrator["Saga Orchestrator - Port 5058"]

    Orchestrator -->|State persistence| SagaDB[("Saga DB - Port 5436")]

    Orchestrator -->|3. ReserveInventoryCommand| InventoryService["Inventory Service - Port 5060"]
    Orchestrator -->|4. ProcessPaymentCommand| PaymentService["Payment Service - Port 5061"]

    InventoryService -->|InventoryReserved / ReservationFailed| RabbitMQ
    PaymentService -->|PaymentProcessed / PaymentFailed| RabbitMQ

    RabbitMQ -->|Replies| Orchestrator
    Orchestrator -->|Compensate: ReleaseInventoryCommand| InventoryService
    Orchestrator -->|OrderCompletedEvent / OrderFailedEvent| OrderService
    Orchestrator -->|OrderCompletedEvent / OrderFailedEvent| CartService["Cart Service - Port 5062"]
    Orchestrator -->|OrderCompletedEvent: confirm the reservation| InventoryService
```

### Microservice Specifications:
* **Project Name**: `Ecommerce.Orchestrator`
* **Directory**: `server/src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/` — one project; its
  `DbContext` lives in the WebApi project too.
* **HTTP Port**: `5058`
* **Dedicated Database**: `ecommerce_saga_db` (PostgreSQL on Port `5436`)
* **Technology**: MassTransit 8 state machine saga (what was once the separate `Automatonymous`
  library), persisted with EF Core and optimistic concurrency.

**There is no `SetOrderCancelled` step.** The saga never tells Order to cancel anything: it publishes
`OrderCompletedEvent` or `OrderFailedEvent`, and Order settles its own row from those. The saga has
two states of its own, `Submitted` and `InventoryReservedState`, and finalizes on either outcome.

`Cancelled` is reachable since [specs/039](../../specs/039-order-cancellation/), but **not through the
saga**: the saga has already ended at payment by the time anybody can cancel. A cancellation is a
request to Order, which publishes `OrderCancelledEvent`; Inventory and Payment each undo their own
part from their own rows. See [After the saga](#-after-the-saga-what-happens-to-a-paid-order-completed).

---

## 3. Implementation Roadmap

The phases below are in the order they were built. The roadmap began with five and grew as each
phase exposed what the next one needed; Phase 6.5 exists because Phase 6 was declared complete while
the order record still disagreed with the saga.

### Status today (2026-09-24)

| Phase | What | Status |
| :--- | :--- | :--- |
| 1 | Shared error handling and validation | Done |
| 2 | Event bus and transactional outbox | Done - every publishing service |
| 3 | Standalone orchestrator (`OrderStateMachine`) | Done |
| 4 | Order service and checkout | Done |
| 5 | Inventory and reservations | Done |
| 6 | Payment and the full saga | Done - **with a stub payment provider** |
| 6.5 | Order learns the saga's outcome | Done |
| 7 | Observability (Seq) and end-to-end verification | Done, except the real payment provider, deferred |
| After | Checkout correctness, fulfilment, cancellation, delivery, seller payouts, audit and notifications | Done - outside the saga, below |

The saga's own shape has not changed since Phase 6: reserve, pay, and release the stock if payment
fails. Everything added since either happens before it (pricing, the cart, the address) or after it
ends (fulfilment, cancellation, delivery, payouts).

---

### 🟢 Phase 1: Global Cross-Cutting Error Handling & Validation (Completed)
* **Goal**: Standardize exception handling and DTO validation across all microservices.
* **Deliverables**:
  1. **FluentValidation + MediatR Pipeline Behavior (`ValidationBehavior`)**: Intercepts requests and validates rules prior to handler execution.
  2. **Clean Exception Hierarchy**: `NotFoundException`, `ValidationException`, `ConflictException`.
  3. **ASP.NET Core 10 `IExceptionHandler`**: Maps custom exceptions to standardized **RFC 7807 `ProblemDetails` JSON** (HTTP 400, 404, 409, 500).
  4. **Environment Masking**: Shows full stack traces in `Development`, masks internal details in `Production`.

---

### 🟢 Phase 2: Event Bus Infrastructure & Transactional Outbox (Completed)
* **Goal**: Integrate asynchronous message bus and Outbox pattern into microservices.
* **Deliverables**:
  1. Standardized `MassTransit.RabbitMQ` and `MassTransit.EntityFrameworkCore` across projects.
  2. Configured MassTransit transactional outbox (`AddTransactionalOutboxEntities()`, `o.UsePostgres()`, `o.UseBusOutbox()`), first in `CatalogDbContext`; today in Identity, Catalog, Order, Orchestrator, Inventory, Payment and Activity. Identity gained MassTransit in specs/027, to announce sellers; Cart only consumes and has no outbox.
  3. Implemented domain event contracts in `Ecommerce.Contracts` (e.g., `ProductCreatedEvent`).

---

### 🟢 Phase 3: Standalone Saga Orchestrator Microservice (`Ecommerce.Orchestrator`) (Completed)
* **Goal**: Implement the central Saga Orchestrator Service.
* **Deliverables**:
  1. Created `server/src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi` (Port `5058`, DB `ecommerce_saga_db` on Port `5436`).
  2. Implemented **MassTransit State Machine Saga (`OrderStateMachine`)**:
     * Happy Path: `Submitted` $\rightarrow$ `ReserveInventoryCommand` $\rightarrow$ `InventoryReserved` $\rightarrow$ `ProcessPaymentCommand` $\rightarrow$ `PaymentProcessed` $\rightarrow$ `OrderCompleted`.
     * Compensation flows: Executing `ReleaseInventoryCommand` on payment failure.

---

### 🟢 Phase 4: Ordering Microservice (`Ecommerce.Order`) & Submit Order Flow (Completed)
* **Goal**: Build operational Order service and API endpoint to initiate checkout.
* **Deliverables**:
  1. **Ordering Microservice (`Ecommerce.Order`)**: Port `5059`, DB `ecommerce_order_db` (Port `5434`).
  2. Implemented `SubmitOrderCommand` API via Transactional Outbox pattern.
  3. Added YARP API Gateway route `/api/orders/{**catch-all}` mapping to Port `5059`.

---

### 🟢 Phase 5: Inventory Microservice (`Ecommerce.Inventory`) & Reservation Flow (Completed)
* **Goal**: Answer the saga's stock request so an order can leave `Submitted`.
* **Deliverables**:
  1. **Inventory Microservice**: Port `5060`, DB `ecommerce_inventory_db` (Port `5437`).
  2. `ReserveInventoryCommand` and `ReleaseInventoryCommand` consumers, replying with
     `InventoryReservedEvent` / `InventoryReservationFailedEvent`.
  3. `OrderCompletedEvent` consumed as the confirmation signal — the contracts carry no
     `ConfirmInventoryCommand`, and the saga finalizes without telling inventory anything.
  4. Idempotent consumers: MassTransit EF inbox plus a unique `(OrderId, ProductId)` constraint.
  5. Expiry sweeper returning stock held by orders that never settled.
  6. First test project in the repository, running against a real PostgreSQL.

> The saga now reaches `InventoryReservedState` and stops there: no payment service exists yet, so
> every reservation is eventually reclaimed by the sweeper. That is correct behaviour, not a defect.

---

### 🟢 Phase 6: Payment Microservice (`Ecommerce.Payment`) & Full Saga Completion (Completed)
* **Goal**: Close checkout — the saga runs all the way to `OrderCompleted`.
* **Deliverables**:
  1. **Payment Microservice**: Port `5061`, DB `ecommerce_payment_db` (Port `5438`).
  2. `ProcessPaymentCommand` consumed, replying `PaymentProcessedEvent` / `PaymentFailedEvent`.
  3. One payment per order, enforced by a unique constraint; the losing side of a race reports the
     outcome that won rather than dropping the message or contradicting it.
  4. `PAYMENT_OUTCOME` makes the **compensation branch reachable** — releasing held stock on a
     failed payment had never actually run before this.

> ⚠️ **The gateway is a stand-in and moves no money.** Marked on every payment row, in the startup
> log, and in `/health`. `Infrastructure/Gateway/` is the seam a real provider replaces.

---

### 🟢 Phase 6.5: Order Lifecycle Visibility (`Ecommerce.Order` learns the outcome) (Completed)
* **Goal**: Make the order record agree with what the saga actually did, and let a shopper read it.
* **Why it needed its own phase**: Phase 6 declared checkout complete, and it was — the payment was
  recorded and the stock was deducted. But the `orders` row was never told. Eleven completed orders
  read `Submitted`, and `OrdersController` had only `POST`, so nothing could show otherwise. Filed
  as issue #2 and specified in [specs/003-order-lifecycle](../../specs/003-order-lifecycle/).
* **Deliverables**:
  1. `OrderCompletedConsumer` and `OrderFailedConsumer` — the first consumers this service has had.
  2. A **guarded transition**: `UPDATE ... WHERE Id = @id AND Status = 'Submitted'`, so a redelivery
     affects zero rows. Mutation-checked — removing the guard fails three tests.
  3. `GET /api/orders` (paged) and `GET /api/orders/{id}`, scoped by `ICurrentUser`. Another
     shopper's order answers **404, not 403**, so the response does not confirm the id is real.
  4. `Ecommerce.Order.Tests` (15 tests) against a real PostgreSQL, and order-ownership assertions in
     the auth smoke script using real signed tokens.

> ⚠️ **Consumer class names become queue names.** Inventory and Order both have a class called
> `OrderCompletedConsumer`, so both bound to a queue named `OrderCompleted` and *competed* for it —
> each completion reached one service or the other. The order settled and the stock stayed held.
> Order now sets an endpoint name prefix. Publish/subscribe fans out per **endpoint**, not per
> service.

At the time, four `OrderStatus` values were deliberately unreachable: `Pending`, `StockReserved`,
`Paid` and `Cancelled`. The saga passes through the middle two but announces neither, and adding an
announcement means changing a shared contract every service deserializes. Documented in
[specs/003 data-model](../../specs/003-order-lifecycle/data-model.md) rather than left to be
rediscovered.

> **Since then**: `Paid` is what a successful checkout settles to (feature 011), and `Cancelled` is
> reached by a request (specs/039). `Pending` and `StockReserved` are still unreachable. `Completed`
> stays in the enum so old rows and a rolled-back image still parse, and every read reports it as
> `Paid`.

---

### 🟢 Phase 7: Observability, Centralized Logging (Seq) & E2E Verification (Completed)
* **Goal**: Operational visibility, centralized logging, and end-to-end system testing.
* **Deliverables**:
  1. ✅ **Seq** in `docker-compose.yml` (UI `5380`, ingestion `5341`) — feature 013.
  2. ✅ Structured logs **and distributed traces** from every service to Seq over OpenTelemetry
     (not Serilog), one trace per checkout across the broker, `OrderId` on every line —
     [specs/013](../../specs/013-observability/), [#22](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/22).
  3. ~~Replace the stub payment gateway with a real provider integration.~~ Moved out of this phase:
     deliberately deferred, together with deployment.
  4. ✅ **End-to-end verification in CI** — [`verify-saga.sh`](../../.github/scripts/verify-saga.sh)
     places a real order over HTTP and follows it through every service it touches, asserting that the stock
     moved by exactly the amount ordered and that nothing is left held. Both branches on every
     change: payment approving, and payment refusing so compensation is exercised. Design and
     evidence in [specs/007-saga-e2e-verification](../../specs/007-saga-e2e-verification/).

> **Why this one came first.** Every other test here is confined to a single service, and the saga's
> characteristic failures are not. The queue-name collision recorded under Phase 6.5 settled an order
> while its stock stayed held, and all fifteen Order tests were green throughout. Seq makes such a
> failure easier to *diagnose*; this makes it *detected*, which has to come first.

> ✅ **Found and fixed on its first run.** The check stalled on the first order after a cold start —
> deterministic, 2 of 2 in CI and 2 of 2 locally. With MassTransit at `Debug` the cause was visible:
> `InventoryReservedEvent` finished in 0.29s while `OrderSubmittedEvent` was still 5.4s from
> committing, so the reply found no saga instance and was discarded silently. The orchestrator was
> the one service publishing **outside** the transactional outbox, which Principle III makes
> non-negotiable. Fixed in `20260921104437_AddTransactionalOutbox`; cold starts now settle in 2–3s,
> 4 of 4. Four sagas stranded between **2026-09-03 and 09-17** show how long it had been happening
> without anyone noticing — the second order of any session always worked.

---

### 🟢 After the saga: checkout correctness (Completed)

Two features that did not change the saga's shape but changed what it is trusted with:

* **[specs/009](../../specs/009-catalog-owns-price/) — Catalog owns the price.** The client used to
  send the unit price and the system charged it; a product listed at 40,000,000 was bought for 1.
  Order now asks Catalog over gRPC at submission and freezes price and name onto the line — the
  system's first synchronous cross-service call.
* **[specs/010](../../specs/010-customer-cart/) — the cart.** Checkout takes no body and reads the
  caller's cart over gRPC. Cart consumes `OrderSubmitted`, `OrderCompleted` and `OrderFailed`, and
  removes ordered lines only on completion — whichever of the first two arrives second applies it,
  because nothing orders delivery across message types.
* **[specs/011](../../specs/011-order-shipping/) — somewhere for the order to go.** Addresses live in
  Identity; checkout names one and a delivery option, and Order freezes a copy of both. The total — and
  so the charge — includes delivery, with no contract change. After the saga the order reads **`Paid`**
  (no longer `Completed`), and staff move it to `Preparing` and `Shipped`. The saga still ends at
  payment: despatch is manual until something can actually despatch.
* **[specs/012](../../specs/012-order-totals/) — a total with something behind it.** The total is
  stored as subtotal, delivery, tax, discount and grand total, with the rate applied; the database
  refuses parts that do not add up. Tax follows the destination; prices exclude it
  ([ADR-002](./adr-002-tax-exclusive-prices.md)). Still no contract change — the saga charges the
  stored grand total.
* **[specs/020](../../specs/020-product-variants/) and [specs/022](../../specs/022-multi-currency-prices/) —
  variants and currencies.** The variant is what is bought, reserved and priced
  (`CatalogPricing.PriceVariants`), and the order's currency travels on `OrderSubmittedEvent` through
  the saga into `ProcessPaymentCommand`, so a payment row says what its amount is in. ⚠️ A service
  that only *relays* a contract must be rebuilt when it grows: an Orchestrator built before
  `OrderItemDto` gained `VariantId` dropped the field while relaying, and the wrong variant's stock
  moved.

---

### 🟢 After the saga: what happens to a paid order (Completed)

The saga ends at payment and has no part in anything below. Each step is a request to Order (or a
timer inside it), settled with a guarded single-statement `UPDATE` under a row lock, and announced
through Order's outbox in the same transaction.

```text
                          saga ends here
Submitted ──(saga)──▶ Paid ──▶ each parcel: Pending ──▶ Preparing ──▶ Shipped ──▶ delivered
     │                  │                                                  (customer, or 7 days)
     ▼                  ▼
   Failed           Cancelled  (customer: while every parcel waits; staff: until the first ships)
```

* **The marketplace** ([specs/027](../../specs/027-seller-accounts/),
  [034](../../specs/034-seller-sales/)). A product may belong to a seller; the order line freezes
  the seller and the shop name at checkout.
* **Fulfilment per seller** ([specs/035](../../specs/035-seller-shipments/)). One `order_shipments`
  row per seller per order, plus one for the shop's own goods. Each seller moves their own parcel to
  `Preparing` and `Shipped`; staff move the shop's. `orders.Status` is rewritten as a summary and
  gained no new value, so a rolled-back image can still read it. Every move locks the order row first
  (`FOR UPDATE`), which is what makes two sellers shipping at once leave the order `Shipped`.
* **Cancellation** ([specs/039](../../specs/039-order-cancellation/)). Order takes the same row lock
  as a parcel move, so a cancel and a ship on the same order serialise and exactly one wins. It
  publishes `OrderCancelledEvent`, which carries no items and no amount: Inventory's
  `RestockCancelledOrderConsumer` puts back what its own reservations say (a confirmed reservation
  goes back on hand; a still-held one, when the cancellation overtook the completion, is released),
  and Payment's `RefundCancelledOrderConsumer` records a refund of what its own payment row says, in
  `refunds`, unique on `OrderId`. The refund moves no money - Payment is a stub.
  This is compensation after the fact, not a saga step: there is nothing left to coordinate, because
  each service can undo its own part from its own data. `verify-saga.sh` cancels a second order and
  asserts the stock back and one full refund.
* **Delivery** ([specs/040](../../specs/040-delivery-confirmation/)). A parcel is delivered when its
  customer says so or when `DeliveryConfirmationSweeper` does so `Delivery:AutoConfirmDays` (7) after
  it shipped. Delivered is columns on the parcel (`ShippedAt`, `DeliveredAt`, `DeliveryConfirmedBy`),
  not a status. Order publishes `ParcelDeliveredEvent`, from which Catalog learns who may review what
  ([specs/046](../../specs/046-product-reviews/)).
* **Seller payouts** ([specs/037](../../specs/037-seller-payouts/)). Each parcel freezes its goods
  total, commission and delivery share at checkout. A delivered parcel of a paid order is due; an
  administrator records a payout that claims the due parcels in one statement. A payout is a ledger
  entry, not a transfer.
* **Audit and notifications** ([specs/041](../../specs/041-audit-log/),
  [042](../../specs/042-in-app-notifications/)). Every step above, and settlement itself, records an
  audit entry and tells the people concerned (the buyer: paid, failed, shipped, cancelled; each
  seller: a new sale, a cancelled sale, a parcel received, a payout). Both are messages to the
  **Activity** service (port `5063`, database `ecommerce_activity_db` on `5440`), staged in the same
  transaction as the change they describe. Settling inside the saga's outcome consumer is where this
  first went wrong - see
  [reliable messaging §2.2](./reliable-messaging-and-outbox-pattern.md#22-a-guarded-statement-in-its-own-transaction-the-stage-callback).

### ⬜ Not built

* **A real payment provider.** `StubPaymentGateway` is the seam; payments, refunds and payouts all
  record money that never moves.
* **Automatic despatch.** Parcels move because a person says so.
* **Partial cancellation or returns.** A cancellation is of a whole order, and only before the first
  parcel ships.
