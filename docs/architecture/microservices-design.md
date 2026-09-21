# E-Commerce Microservices Architecture Design

This document describes the service boundaries, databases and communication patterns of the system
**as built**. It began as a proposal; where the proposal and the code parted ways, the section says
so, and ideas that were never built are listed under [Proposed, not built](#6-proposed-not-built).

---

## 1. System Topology (Mermaid)

```mermaid
graph TD
    Client[Client] -->|HTTP| Gateway[API Gateway - YARP :5000]

    Gateway -->|REST| Identity[Identity :5056]
    Gateway -->|REST| Catalog[Catalog :5057]
    Gateway -->|REST| Order[Order :5059]
    Gateway -->|REST| Inventory[Inventory :5060]
    Gateway -->|REST| Payment[Payment :5061]
    Gateway -->|REST| Cart[Cart :5062]

    Order -->|gRPC GetMyCart| Cart
    Order -->|gRPC GetPrices| Catalog
    Cart -->|gRPC DescribeProducts| Catalog

    Order -->|OrderSubmittedEvent| RabbitMQ[RabbitMQ]
    RabbitMQ --> Orchestrator[Saga Orchestrator :5058]
    Orchestrator -->|ReserveInventory / ReleaseInventory| Inventory
    Orchestrator -->|ProcessPayment| Payment
    Orchestrator -->|OrderCompleted / OrderFailed| Order
    Orchestrator -->|OrderCompleted / OrderFailed| Cart
    Orchestrator -->|OrderCompleted| Inventory
    Inventory -->|StockAvailabilityChanged| Catalog

    Identity --> DB1[(identity_db)]
    Catalog --> DB2[(catalog_db)]
    Order --> DB3[(order_db)]
    Orchestrator --> DB4[(saga_db)]
    Inventory --> DB5[(inventory_db)]
    Payment --> DB6[(payment_db)]
    Cart --> DB7[(cart_db)]
```

Every database is PostgreSQL, one per service. Every arrow into RabbitMQ is published through the
transactional outbox.

---

## 2. Microservice Module Breakdown

Each microservice is fully self-contained, owning its business logic, database, and scaling profile.

### 2.1 Identity & Auth Service (Present)
* **Responsibility**: User management, authentication, role assignment, token validation (Access + Refresh tokens).
* **Database**: `ecommerce-identity-db` (Postgres).
* **Key Entities**: `User`, `Role`, `RefreshToken`.

### 2.2 Product Catalog Service
* **Responsibility**: Managing brands, categories, dynamic product specifications, pricing, search indexes, and media.
* **Database**: `ecommerce_catalog_db` (PostgreSQL, port `5433`).
* **Key Entities**: `Product`, `Category`. The **owner of price**: checkout asks Catalog over gRPC
  (`GetPrices`) and freezes the answer onto the order line.
* **Stock**: holds only a read model, `Availability` (`InStock` / `OutOfStock`), fed by Inventory —
  see [Stock ownership](#stock-ownership).
* No cache, and no brands or dynamic attributes — see [Proposed, not built](#6-proposed-not-built).

#### 2.3 Ordering Service (Implemented: `Ecommerce.Order`)
* **Responsibility**: Checkout — reads the caller's cart from Cart and the prices from Catalog, creates
  the order, and publishes `OrderSubmittedEvent`. Settles the order on the saga's outcome and serves
  owner-scoped reads. It does **not** hold the cart.
* **Database**: `ecommerce-order-db` (Port `5434`, Postgres, due to strong transactional ACID requirements).
* **Key Entities**: `Order`, `OrderItem`.

### 2.4 Saga Orchestrator Service (Implemented: `Ecommerce.Orchestrator`)
* **Responsibility**: Standalone microservice running MassTransit `OrderStateMachine` Saga to coordinate multi-service distributed transactions (Inventory reservation, Payment processing, and Compensating Transactions).
* **Database**: `ecommerce_saga_db` (container `ecommerce-orchestrator-db`, port `5436`, storing the saga's state).
* **Key Entities**: `OrderStateData`.

### 2.5 Inventory Service (Implemented: `Ecommerce.Inventory`)
* **Responsibility**: The sole authority on sellable quantity. Reserves stock for the saga, releases it
  on compensation, confirms it when an order completes, and returns abandoned holds with an expiry
  sweeper.
* **Database**: `ecommerce_inventory_db` (PostgreSQL, port `5437`). Reservations lock **one aggregated
  row per product** with `SELECT … FOR UPDATE`, taken in `ProductId` order so two orders cannot
  deadlock. No Redis. The unit-pool alternative is in
  [concepts](../concepts/shopify-inventory-skip-locked-pattern.md), studied and not adopted.
* **Key Entities**: `StockItem`, `StockReservation`.

### 2.6 Payment Service (Implemented: `Ecommerce.Payment`)
* **Responsibility**: Answers the saga's `ProcessPaymentCommand`. **A stub**: it approves (or, with
  `PAYMENT_OUTCOME=Reject`, refuses) without contacting any provider, and says so on every row, in
  its startup log and in `/health`.
* **Database**: `ecommerce_payment_db` (PostgreSQL, port `5438`), one payment per order.
* **Key Entities**: `Payment`.

### 2.7 Cart Service (Implemented: `Ecommerce.Cart`)
* **Responsibility**: One cart per signed-in customer, outliving a session. **Stores no price** — it
  asks Catalog (`DescribeProducts`) when read. Serves `GetMyCart` to Order at checkout, identifying the
  customer from the forwarded token, and removes ordered lines when the order **completes**.
* **Database**: `ecommerce_cart_db` (PostgreSQL, port `5439`).
* **Key Entities**: `Cart`, `CartLine`, `CheckoutOutcome`.
* Design and the out-of-order event handling: [specs/010](../../specs/010-customer-cart/).

---

## 3. Communication Patterns

### 3.1 Asynchronous Event-Driven & Saga Orchestration (Broker: RabbitMQ)
Used when a service needs to trigger actions in other services without waiting for a response, ensuring high resilience, eventual consistency, and compensation rollback.

```text
Ordering Service (Submit Order)
      │
      ▼ (OrderSubmittedEvent via Transactional Outbox)
RabbitMQ Broker ──► Saga Orchestrator (OrderStateMachine)
                          │
         ┌────────────────┴────────────────┐
         ▼                                 ▼
[ReserveInventoryCommand]        [ProcessPaymentCommand]
         │                                 │
         ▼                                 ▼
  [Inventory Service]              [Payment Service]
 (Reserve Bounded Stock)          (Process Charge)
         │                                 │
         └─────────────┬───────────────────┘
                       ▼ (Failure Compensation)
         [ReleaseInventoryCommand] ──► Rollback Inventory
```

### 3.2 Synchronous calls (gRPC)
There are exactly three, all reads, all on checkout's path or the cart's:

| Caller | Callee | RPC | Why it cannot be a message |
| :--- | :--- | :--- | :--- |
| Order | Cart | `GetMyCart` | what is being bought has to be known before the order exists |
| Order | Catalog | `GetPrices` | the price is a decision about money, taken at the moment of sale |
| Cart | Catalog | `DescribeProducts` | showing today's name and price; the cart renders without it if Catalog is down |

They run over **h2c on a second port** (Catalog `6057`, Cart `6062`), because one plaintext port
cannot serve both HTTP/1.1 and HTTP/2. The cost is real: Catalog or Cart being down stops checkout
(`503`). The reasoning and the measurements are in
[How Services Talk to Each Other](./service-to-service-communication.md).

**Order does not ask Inventory whether something is in stock.** An answer read before the
reservation is already stale by the time it is acted on; the only check that counts is the
reservation itself, taken under a row lock inside the saga.

---

## 4. API Gateway Configuration (YARP)

We use Microsoft's official **YARP (Yet Another Reverse Proxy)** library running in [`Ecommerce.ApiGateway`](../../server/src/ApiGateway/Ecommerce.ApiGateway/) on **Port `5000`**.

The routes live in [`appsettings.json`](../../server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json),
which is the source of truth — the excerpt below shows the shape, not the full list. Every service
has a route **and** a cluster, and each `/api/<svc>/health` route rewrites to that service's
`/health`. Adding a service without both is the usual reason a new endpoint answers 404 through the
gateway and 200 directly.

### Route mappings (excerpt)

```json
  "ReverseProxy": {
    "Routes": {
      "identity-route": {
        "ClusterId": "identity-cluster",
        "Match": { "Path": "/api/auth/{**catch-all}" }
      },
      "catalog-categories-route": {
        "ClusterId": "catalog-cluster",
        "Match": { "Path": "/api/categories/{**catch-all}" }
      },
      "catalog-products-route": {
        "ClusterId": "catalog-cluster",
        "Match": { "Path": "/api/products/{**catch-all}" }
      },
      "order-route": {
        "ClusterId": "order-cluster",
        "Match": { "Path": "/api/orders/{**catch-all}" }
      }
    },
    "Clusters": {
      "identity-cluster": {
        "Destinations": { "destination1": { "Address": "http://localhost:5056/" } }
      },
      "catalog-cluster": {
        "Destinations": { "destination1": { "Address": "http://localhost:5057/" } }
      },
      "order-cluster": {
        "Destinations": { "destination1": { "Address": "http://localhost:5059/" } }
      }
    }
  }
```

---

## 4.5 Runtime Topology — How the Services Actually Run

Changed on 2026-09-17 ([spec 005](../../specs/005-containerise-services/)). Until then the design
described above only ever ran in one place: a developer's own machine. Every connection string
hardcoded `Host=localhost`, every service pinned `app.Run("http://localhost:PORT")`, and there was
no artifact at all — running the system elsewhere meant copying the source and building it again.

### The shift: configuration moves out of the code

| | Before | After |
| :--- | :--- | :--- |
| Database address | `Host=localhost` in six `Program.cs` | `DB_HOST`, defaulting to `localhost` |
| Listen address | `app.Run("http://localhost:5057")` | `ASPNETCORE_URLS`, falling back to the pinned address |
| Settings precedence | `.env` **overrode** the environment | environment **overrides** `.env` |
| Artifact | none | one image per service |
| Schema creation | `dotnet ef` from the host, only | that, or `RUN_MIGRATIONS_ON_STARTUP` in a container |

The precedence inversion is the one that mattered architecturally. A settings file that wins over
the environment makes an image unconfigurable: whatever a container is told at run time would be
overridden by a file inside it, and the same image would behave identically everywhere. Fixing it is
what made "build once, run anywhere with different settings" possible at all.

### Two run modes, deliberately both supported

```text
docker compose up -d                          infrastructure only  -> start-dev.sh, debugger attached
  + -f docker-compose.app.yml                 everything           -> one command, nothing on the host
```

The services live in an **overlay** file rather than in `docker-compose.yml`. Merging them would
force every contributor down the container path; keeping them apart means the existing workflow is
untouched. That is why every new setting defaults to today's local value rather than to a container
value.

### What each service looks like now

- One [Dockerfile](../../server/Dockerfile) builds every service, selected by a `PROJECT` build
  argument. One file per service would drift — a fix applied to all but one is invisible and nothing
  fails. It copies each `.csproj` by name, so a new project needs a line there too.
- Inside its container a service binds `8080`; compose maps that to the port it has always used on
  the host, so the gateway routes, the port table and every guide stay true.
- The gateway is the only service whose configuration genuinely differs between the two modes,
  because it is the only one that needs to know where the *others* are.
- Services wait for their dependencies through `depends_on: condition: service_healthy` plus a
  connection retry. Containers start in parallel; a service reaching its database a moment early is
  normal, not a failure.

### What this deliberately does not do

It makes images and runs them locally. It does **not** publish them, tag them, or let you roll back
to an earlier one — that is [#8](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/8).

And rolling back an image does **not** undo a migration. This design has already produced one
example: dropping `products.StockQuantity` means any Catalog build from before that change now fails
against the schema. Making schema changes survive a rollback needs expand/contract as a rule, which
is [#9](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/9).

Operational detail lives in [Running in Containers](../infrastructure/running-in-containers.md);
step-by-step startup is in [Getting Started](../guides/getting-started.md).

---

## 5. Key Patterns

All three are in place:
1. **Saga Pattern (Orchestration)**: a dedicated orchestrator coordinates reservation, payment and
   compensation — [saga roadmap](./saga-orchestration-roadmap.md).
2. **API Gateway (YARP)**: one entry point on port `5000`.
3. **Transactional Outbox**: every service that publishes writes the row and the message in one
   transaction — [reliable messaging](./reliable-messaging-and-outbox-pattern.md).

## 6. Proposed, not built

The original proposal included these. None exists; each is listed so that a reader does not look for
it in the code.

- **Notification service** (emails for registration, invoices, shipment).
- **MongoDB for Catalog**, and **brands / dynamic product attributes**.
- **Redis** — as a Catalog cache, or for inventory locking.
- **Order asking Inventory for stock over gRPC** — replaced by reservation inside the saga (§3.2).

---

## Stock ownership

**Inventory is the sole authority on sellable quantity.** It learns a product exists from
`ProductCreatedEvent`, registers it at zero, and staff set quantities through Inventory.
`GET /api/stock/{productId}` is where a real number comes from; it is public.

Catalog holds `Product.Availability` — a **read model**, fed by `StockAvailabilityChangedEvent`
from Inventory, exposed as `"InStock"` / `"OutOfStock"` and never as a count. Nothing sells against
it: checkout reserves under `FOR UPDATE` against Inventory's row, and it must stay that way, because
a read model fed by messages is seconds behind by design.

### What this section used to say, and why it was wrong

It used to say `Product.StockQuantity` was "descriptive only", that the field was not removed
because "that would be a breaking API change", and that "nothing may read it for an availability
decision".

The first two were accurate. The third was not enforceable, and in practice was false: the field was
returned by `GET /api/products`, which is `[AllowAnonymous]`, so every shopper read it while deciding
whether to buy. **A shopper's decision to buy is an availability decision** — the most important one
there is. Meanwhile the number could never change: Catalog had no update command and consumed no
messages, so it stayed at whatever was typed at creation. Measured on 2026-09-17, one product read
50 in the catalogue while Inventory went from 10 to 8.

Filed as [#4](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/4) and fixed in
[specs/004-stock-single-source](../../specs/004-stock-single-source/). The breaking change was taken:
there is no frontend in this repository, so a caller that breaks finds out immediately, whereas a
caller reading a stale number never does.

The lesson worth keeping is not about stock. It is that **"nothing may read this" is a wish, not a
constraint.** A duplicated value that is reachable will be read. See
[constitution.md](../../.specify/memory/constitution.md) principle I.
