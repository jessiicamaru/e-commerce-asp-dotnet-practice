# E-Commerce Documentation

Documentation for the .NET 10 microservices backend: seven services behind a YARP gateway, one
PostgreSQL database each, RabbitMQ between them, and a saga orchestrating checkout.

Everything under `architecture/`, `features/`, `infrastructure/` and `guides/` describes the system
**as it runs** — where it once described a plan instead, the page now says what was built. Study
notes about designs that were *not* adopted live separately, under `concepts/`. Feature-by-feature
design records (why each decision was taken, and what it was measured against) are in
[`specs/`](../specs/).

---

## 🗺️ Documentation Map

### 🛠️ Start here
* [**Getting Started**](./guides/getting-started.md): run the system — all in containers, or infrastructure in Docker with the services on your machine — check it is really up, place an order end to end, and run the tests.
* [**Bruno collection**](../bruno/): every public endpoint, runnable and tested, through the gateway. Open the folder in Bruno, pick the `local` environment, fill the two secret admin variables, run it top to bottom.
* [**Observability**](./guides/observability.md): Seq, one trace per checkout, and the queries that answer "what happened to order X".
* [**Troubleshooting**](./guides/troubleshooting.md): compile errors, EF Core key generation, a local install or a stray process shadowing a container, and the container-specific traps.

### 🏛️ Architecture
* [**Microservices Design**](./architecture/microservices-design.md): each service as built — responsibility, database, entities — the topology, the three synchronous calls, who owns stock, and what was proposed but never built.
* [**How Services Talk to Each Other**](./architecture/service-to-service-communication.md): why the system went from zero synchronous calls to three, why gRPC runs on a second port (h2c), what that costs checkout, and why none of it touches messaging.
* [**Saga Orchestration & Roadmap**](./architecture/saga-orchestration-roadmap.md): the checkout saga, its compensation, and the phases that built the system — including what each phase found wrong with the one before.
* [**Reliable Messaging & Outbox**](./architecture/reliable-messaging-and-outbox-pattern.md): the transactional outbox, how every consumer survives duplicates *and* out-of-order delivery, and what failure handling is — and is not yet — configured.
* [**Error Handling & `Ecommerce.Shared`**](./architecture/error-handling-and-shared-building-block.md): RFC 7807 responses, the exception-to-status map, request validation, and the lessons recorded against them.
* [**Overview & Scope**](./architecture/architecture-overview.md): target modules, technology stack, and which parts of that scope are done.
* [**ADR-001: UUID v7 primary keys**](./architecture/adr-001-uuidv7-primary-keys.md): why time-ordered UUIDs, what they reveal, and how far the code has caught up.
* [**ADR-002: Prices exclude tax**](./architecture/adr-002-tax-exclusive-prices.md): how a total is reached — tax by destination, per line and on delivery, rounded half away from zero — and why not tax-inclusive prices.

### 🔑 Authentication (Identity)
* [**JWT Setup**](./features/auth/jwt-setup.md): who signs and who validates, the three settings that break silently, and who may call which endpoint.
* [**Token Storage & Refresh**](./features/auth/security-best-practices.md): access token in the body, refresh token in an HttpOnly cookie, rotation — and three known weaknesses of the current code.
* [**CQRS & MediatR Guide**](./features/auth/cqrs-guide.md): how the register and login commands are built.
* [**Database Schema**](./features/auth/db-design.md): users, roles and refresh tokens, how the administrator is bootstrapped, and where the real schema differs from the logical one.

### 🔌 Infrastructure
* [**Running in Containers**](./infrastructure/running-in-containers.md): the container path — configuration precedence, the three settings that are easy to get wrong, migrations at startup, images and secret scanning, published images.
* [**Database Setup**](./infrastructure/database-setup.md): one PostgreSQL per service, host versus container ports, EF Core migration commands, pgAdmin.
* [**RabbitMQ Setup**](./infrastructure/rabbitmq-setup.md): the broker, its management console, and the two password variable names.

### ⚙️ Continuous Integration
* **[`.github/workflows/ci.yml`](../.github/workflows/ci.yml)**: builds and tests on every push and pull request to `main`, then runs **two** smoke jobs side by side against real PostgreSQL and RabbitMQ — `Saga end-to-end` (an order placed through the cart, followed through every service it touches) and `Auth smoke test` (anonymous → `401`, `Customer` on an Admin endpoint → `403`, `Admin` → through).
* **Releases**: a merge to `main` whose checks pass publishes one image per service to GHCR as `ghcr.io/jessiicamaru/ecommerce-<service>:sha-<short-sha>`. That name is **enforced** to mean one thing forever: the release skips a name that already resolves, so re-running a publish finishes a partial release instead of rewriting a complete one, and the job ends by checking every name exists. `:main` moves and may never name anything deployable. Guarantees and the one remaining hole: [publish-behaviour.md](../specs/008-immutable-release-tags/contracts/publish-behaviour.md).
* **[`check-schema-compatibility.sh`](../.github/scripts/check-schema-compatibility.sh)**: comments on a pull request whose migration drops, renames or narrows the schema — an earlier image cannot run against it, so rolling back would take the service down. It never blocks.
* **[`verify-image-has-no-secrets.sh`](../.github/scripts/verify-image-has-no-secrets.sh)**: asserts no credential is in **any layer** of an image, not merely its final filesystem — a file deleted in a later layer is still readable in an earlier one. A leaked credential in a published image can only be rotated, never un-published.
* **[`verify-saga.sh`](../.github/scripts/verify-saga.sh)**: the only check that sees **between** services. It places a real order with a real customer token, follows it to a terminal state, and asserts stock on-hand **and** reserved, and the cart afterwards — on both branches, payment approving and refusing. It exists because every other test is confined to one service: two services once shared a queue by accident, the order settled, the stock stayed held, and every unit test was green. Without every service on the checkout path it reports **skipped**, never a quiet pass.
  ```bash
  cd server
  ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
  ```
* **[`verify-auth.sh`](../.github/scripts/verify-auth.sh)**: the auth assertions CI runs, runnable locally against started services:
  ```bash
  cd server
  ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-auth.sh
  ```

### 📚 Concepts — study notes, not the running system
Each opens with what was built instead. Read them for the idea, not as a description of the code.
* [**`FOR UPDATE SKIP LOCKED` unit pools**](./concepts/shopify-inventory-skip-locked-pattern.md): Shopify's flash-sale reservation design — **studied and not adopted**; Inventory locks one aggregated row per product, and the note says when to revisit.
* [**PACELC trade-offs**](./concepts/pacelc-theorem-tradeoffs.md): which parts should prefer availability and which consistency — and where the running system has already moved.

---

## 🚀 Quick Start (Automated One-Click Startup)

First, create `server/.env` from the template and fill in the values:

```bash
cd server
cp .env.example .env
```

`ADMIN_EMAIL` and `ADMIN_PASSWORD` seed the first administrator on Identity startup — without them
no account can reach the Admin-only endpoints. `JWT_SECRET` must be at least 32 characters and the
same for every service: Identity signs tokens with it and all the others validate with it.

Then, from the `server/` directory:

### Option A: PowerShell (Windows)
```powershell
./start-dev.ps1
```

### Option B: Git Bash / Linux
```bash
./start-dev.sh
```

---

## 🌐 Active Service Ports & Endpoints

| Service Name | Port | Base URL / Dashboard |
| :--- | :--- | :--- |
| **API Gateway (YARP)** | `5000` | `http://localhost:5000` |
| **Identity Service** | `5056` | `http://localhost:5056` |
| **Catalog Service** | `5057` | `http://localhost:5057` |
| **Orchestrator Service (Saga)** | `5058` | `http://localhost:5058` |
| **Order Service** | `5059` | `http://localhost:5059` |
| **Inventory Service** | `5060` | `http://localhost:5060` |
| **Payment Service** | `5061` | `http://localhost:5061` (**stub — moves no money**) |
| **Cart Service** | `5062` | `http://localhost:5062` |
| **pgAdmin (DB GUI)** | `5050` | `http://localhost:5050` (`PGADMIN_EMAIL` / `PGADMIN_PASSWORD` from `.env`) |
| **RabbitMQ Management** | `15672` | `http://localhost:15672` (`RABBITMQ_USER` / `RABBITMQ_PASSWORD` from `.env`) |

Identity, Catalog and Cart also serve **gRPC** on a second port — `6056`, `6057` and `6062` — for the calls checkout
makes between services. Nothing outside the system calls them; see
[How Services Talk to Each Other](./architecture/service-to-service-communication.md).

> Anything installed natively on your machine that shares a port with a container will shadow it
> silently. The Identity database is published on `5435` instead of `5432` for that reason, and a
> locally installed RabbitMQ on `5672` will take precedence over the container. See
> [Troubleshooting §6](./guides/troubleshooting.md) if migrations report "already up to date"
> against an empty database, or if CI fails where your machine passes.

