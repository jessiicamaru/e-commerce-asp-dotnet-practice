# E-Commerce Project Documentation Index

Welcome to the documentation for the E-Commerce Clean Architecture ASP.NET Core project. This directory contains detailed guides, designs, and architectural roadmaps for building the backend services.

---

## 🗺️ Documentation Map

### 🏛️ Architecture & Roadmap
* [**Roadmap & Overview**](./architecture/architecture-overview.md): High-level system modules, technology stack, and iterative development phases.
* [**Microservices Design**](./architecture/microservices-design.md): Brainstorming architecture, boundaries, and communication patterns for other modules.
* [**High-Scalability Inventory Reservations (`FOR UPDATE SKIP LOCKED`)**](./architecture/shopify-inventory-skip-locked-pattern.md): Architectural guide on Shopify's unit row pool & PostgreSQL FOR UPDATE SKIP LOCKED strategy for conflict-free flash sale inventory reservations.
* [**PACELC Theorem & Domain-Driven Trade-offs**](./architecture/pacelc-theorem-tradeoffs.md): Architectural analysis of PACELC theorem trade-offs (PA/EL for Catalog vs PC/EC for Saga & Checkout).
* **Cart** ([specs/010-customer-cart](../specs/010-customer-cart/)): the eighth service. One cart per signed-in customer that outlives a session, **storing no price** — it asks Catalog when read, and checkout charges Catalog's price regardless. Checkout reads the cart over gRPC with the customer's own token forwarded, and the ordered lines leave the cart when the order **completes**, so a declined payment leaves it intact. Because `OrderCompletedEvent` carries only an order id, the cart also listens to `OrderSubmittedEvent`, and whichever arrives second applies the removal — nothing orders delivery across message types, which is what issue #15 was.
* [**How Services Talk to Each Other**](./architecture/service-to-service-communication.md): What was true before feature 009 — **zero** synchronous cross-service calls, verified — and the decision that changed it: Order now asks Catalog for the price over gRPC (h2c, Catalog's second port 6057), because the price used to come from the customer's own request. Why fixing [#18](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/18) needs the first one, what REST and gRPC each cost here (with a measurement showing the current endpoints serve HTTP/1.1 only, so gRPC would not connect), what real systems actually do about HTTP/2 and TLS, and why none of it touches messaging.
* [**Reliable Messaging & Outbox Pattern**](./architecture/reliable-messaging-and-outbox-pattern.md): Deep-dive guide on Transactional Outbox, Publisher/Consumer ACK protocol, RabbitMQ queue durability, and Saga Compensation.
* [**Global Cross-Cutting Error Handling & Shared Building Blocks**](./architecture/error-handling-and-shared-building-block.md): Architectural guide on RFC 7807 ProblemDetails error handling, MediatR pipeline validation, and the Ecommerce.Shared building block.
* [**Saga Orchestration & System Roadmap**](./architecture/saga-orchestration-roadmap.md): Comprehensive guide on the Saga Pattern (Orchestration vs Choreography), Standalone Saga Orchestrator Service architecture, and 5-phase master roadmap.
* [**ADR-001: Primary Key Strategy (UUID v7 vs Auto-Increment)**](./architecture/adr-001-uuidv7-primary-keys.md): Architecture Decision Record comparing UUID v4, Auto-Increment IDs, and sequential UUID v7.

### 🔌 Infrastructure & Docker
* [**Database Setup**](./infrastructure/database-setup.md): Guide on running local PostgreSQL and pgAdmin containers, and working with EF Core migrations.
* [**RabbitMQ Setup**](./infrastructure/rabbitmq-setup.md): Guide on running RabbitMQ via Docker Compose and using the Web Management Console to monitor queues.
* [**Running in Containers**](./infrastructure/running-in-containers.md): The two supported ways to run the system — `docker compose` for everything, or infrastructure plus `start-dev.sh` as before. Covers the configuration surface, the three settings that are easy to get wrong, why migrations run at startup only in containers, and how an image is checked for secrets.

### 🔑 Authentication Feature Module
* [**Database Schema Design**](./features/auth/db-design.md): SQL schemas, entities mapping, and data dictionary for users, roles, and refresh tokens.
* [**CQRS & MediatR Guide**](./features/auth/cqrs-guide.md): Details on command handlers, MediatR registration, and presentation mapping.
* [**JWT Middleware Configuration**](./features/auth/jwt-setup.md): Package checklist and middleware registration details to validate access tokens.
* [**Security & Token Storage Best Practices**](./features/auth/security-best-practices.md): Deep dive into XSS/CSRF token vulnerabilities and implementing the HttpOnly cookie hybrid flow.

### ⚙️ Continuous Integration
* **[`.github/workflows/ci.yml`](../.github/workflows/ci.yml)**: Builds the solution on every push and pull request to `main`, then runs **two** smoke jobs side by side against real PostgreSQL service containers and a real RabbitMQ — `Saga end-to-end`, which places an order and follows it through all six services, and `Auth smoke test`, which — it logs in as the seeded administrator and asserts that anonymous callers get `401`, a `Customer` gets `403` on Admin-only endpoints, and an `Admin` gets through.
* **Releases**: a merge to `main` whose checks pass publishes seven images to GHCR as `ghcr.io/jessiicamaru/ecommerce-<service>:sha-<short-sha>`. That name is **enforced** to mean one thing forever — the release asks the registry before pushing and skips a name that already resolves, so re-running a publish can never rewrite an existing release and is therefore the correct way to finish one that stopped partway. The job ends by asking the registry whether all seven names exist, so a green publish means the release is whole rather than that the steps ran. `:main` moves by design and may never name anything deployable. Guarantees, non-guarantees and the one remaining hole are in [specs/008-immutable-release-tags/contracts/publish-behaviour.md](../specs/008-immutable-release-tags/contracts/publish-behaviour.md).
* **[`.github/scripts/check-schema-compatibility.sh`](../.github/scripts/check-schema-compatibility.sh)**: On every pull request, reads the migrations it adds and comments when one drops, renames or narrows part of the schema — because an image built before that change cannot run against it, so redeploying an earlier version would take the service down rather than restore it. It never fails the build: nothing is deployed yet, and a guard people learn to override is worse than none.
* **[`.github/scripts/verify-image-has-no-secrets.sh`](../.github/scripts/verify-image-has-no-secrets.sh)**: Builds one service image and asserts that no credential appears in **any layer** — not merely in the final filesystem, because a file deleted in a later layer is still readable in the earlier one. CI runs it on every push. This is the only automated check in the container work, because a credential that reaches a published image cannot be un-published, only rotated.
* **[`.github/scripts/verify-saga.sh`](../.github/scripts/verify-saga.sh)**: The only check that can see **between** services. It places a real order over HTTP with a real signed customer token, follows it to a terminal state, and asserts that the stock actually moved — on-hand **and** reserved, never the derived `available` alone. Every other test in this repository is confined to one service, which is why Inventory and Order could once both declare a consumer class named `OrderCompletedConsumer`, bind to the same queue, and compete for the event: the order settled, the stock stayed held, and all fifteen Order tests were green. Runs both branches — payment approving, and payment refusing so the compensation path returns the held units. Needs all six services; when it does not have them it reports **skipped** rather than passing quietly.
  ```bash
  cd server
  ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
  ```
* **[`.github/scripts/verify-auth.sh`](../.github/scripts/verify-auth.sh)**: The assertions CI runs. Runnable locally too, against services started with `start-dev`:
  ```bash
  cd server
  ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-auth.sh
  ```

### 🛠️ Developer Guides
* [**Getting Started**](./guides/getting-started.md): **Start here.** The two supported ways to run the project — everything in containers, or infrastructure in Docker with the services on your machine — plus configuring `.env`, checking the system is really up, placing an order end to end, and running the tests.
* [**Bruno collection**](../bruno/): every public endpoint as a runnable [Bruno](https://www.usebruno.com/) collection, through the gateway, with tests on each request and scripts that carry tokens and ids from one request to the next. Open the folder in Bruno, choose the `local` environment, fill the two secret admin variables, and run it top to bottom. Updated in the same change as any endpoint.
* [**Troubleshooting Guide**](./guides/troubleshooting.md): Diagnosis and solutions for common C# compiler warnings, NuGet extension methods, EF Core concurrency exceptions, a locally installed service shadowing a container, and the container-specific traps.

---

## 🚀 Quick Start (Automated One-Click Startup)

First, create `server/.env` from the template and fill in the values:

```bash
cd server
cp .env.example .env
```

`ADMIN_EMAIL` and `ADMIN_PASSWORD` seed the first administrator on Identity startup — without them
no account can reach the Admin-only endpoints. `JWT_SECRET` must be at least 32 characters, and is
now required by Catalog and Order as well, not just Identity.

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
| **pgAdmin (DB GUI)** | `5050` | `http://localhost:5050` (`admin@admin.com` / `123456`) |
| **RabbitMQ Management** | `15672` | `http://localhost:15672` (`guest` / `guest`) |

> Anything installed natively on your machine that shares a port with a container will shadow it
> silently. The Identity database is published on `5435` instead of `5432` for that reason, and a
> locally installed RabbitMQ on `5672` will take precedence over the container. See
> [Troubleshooting §6](./guides/troubleshooting.md) if migrations report "already up to date"
> against an empty database, or if CI fails where your machine passes.

