# E-Commerce Project Documentation Index

Welcome to the documentation for the E-Commerce Clean Architecture ASP.NET Core project. This directory contains detailed guides, designs, and architectural roadmaps for building the backend services.

---

## 🗺️ Documentation Map

### 🏛️ Architecture & Roadmap
* [**Roadmap & Overview**](./architecture/architecture-overview.md): High-level system modules, technology stack, and iterative development phases.
* [**Microservices Design**](./architecture/microservices-design.md): Brainstorming architecture, boundaries, and communication patterns for other modules.
* [**High-Scalability Inventory Reservations (`FOR UPDATE SKIP LOCKED`)**](./architecture/shopify-inventory-skip-locked-pattern.md): Architectural guide on Shopify's unit row pool & PostgreSQL FOR UPDATE SKIP LOCKED strategy for conflict-free flash sale inventory reservations.
* [**PACELC Theorem & Domain-Driven Trade-offs**](./architecture/pacelc-theorem-tradeoffs.md): Architectural analysis of PACELC theorem trade-offs (PA/EL for Catalog vs PC/EC for Saga & Checkout).
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
* **[`.github/workflows/ci.yml`](../.github/workflows/ci.yml)**: Builds the solution on every push and pull request to `main`, then runs an auth smoke test against real PostgreSQL service containers — it logs in as the seeded administrator and asserts that anonymous callers get `401`, a `Customer` gets `403` on Admin-only endpoints, and an `Admin` gets through.
* **[`.github/scripts/verify-image-has-no-secrets.sh`](../.github/scripts/verify-image-has-no-secrets.sh)**: Builds one service image and asserts that no credential appears in **any layer** — not merely in the final filesystem, because a file deleted in a later layer is still readable in the earlier one. CI runs it on every push. This is the only automated check in the container work, because a credential that reaches a published image cannot be un-published, only rotated.
* **[`.github/scripts/verify-auth.sh`](../.github/scripts/verify-auth.sh)**: The assertions CI runs. Runnable locally too, against services started with `start-dev`:
  ```bash
  cd server
  ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-auth.sh
  ```

### 📓 Work Log
Dated records of what changed, what was decided, and what turned out to be wrong. Read the most
recent one before planning: the "what is still not true" section at the end is the part that saves
time.
* [**2026-09-17**](./journal/2026-09-17.md): Order lifecycle visibility (#3) and stock single-source (#5) merged; containers opened as #10; issues #6–#9 filed. Includes two defects that reached a running system and passed every unit test — a queue-name collision between two services, and a verification script that reported clean on an image that provably leaked.

### 🛠️ Developer Guides
* [**Troubleshooting Guide**](./guides/troubleshooting.md): Diagnosis and solutions for common C# compiler warnings, NuGet extension methods, directory mapping, and EF Core concurrency exceptions.

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

