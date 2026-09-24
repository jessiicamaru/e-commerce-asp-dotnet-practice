# Database Setup & Migrations (Database per Service)

This guide explains how to set up, run, and manage isolated PostgreSQL databases using Docker Compose and Entity Framework Core (EF Core) Migrations in our Monorepo Microservices Architecture.

---

## 1. Database per Service Isolation

Each of the eight services owns a dedicated, isolated PostgreSQL 16 database, in its own container
with its own volume. No service reads another's database; a value that crosses a boundary (a product
id on an order line, say) is a copy, not a foreign key.

| Microservice | Container Name | Host port | Database Name | Volume | Tables |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Identity** | `ecommerce-identity-db` | `5435` | `ecommerce_identity_db` | `postgres_identity_data` | users, roles, user roles, refresh tokens, delivery addresses, seller profiles, shop applications |
| **Catalog** | `ecommerce-catalog-db` | `5433` | `ecommerce_catalog_db` | `postgres_catalog_data` | products, variants, options, prices per currency, translations, categories, sellers (read model), reviews, review eligibility, product views |
| **Order** | `ecommerce-order-db` | `5434` | `ecommerce_order_db` | `postgres_order_data` | orders, order items, order shipments (parcels), payouts |
| **Orchestrator** | `ecommerce-orchestrator-db` | `5436` | `ecommerce_saga_db` | `postgres_orchestrator_data` | the saga's state (`order_state_data`) |
| **Inventory** | `ecommerce-inventory-db` | `5437` | `ecommerce_inventory_db` | `postgres_inventory_data` | stock items, reservations |
| **Payment** | `ecommerce-payment-db` | `5438` | `ecommerce_payment_db` | `postgres_payment_data` | payments, refunds |
| **Cart** | `ecommerce-cart-db` | `5439` | `ecommerce_cart_db` | `postgres_cart_data` | carts, cart lines, checkout outcomes |
| **Activity** | `ecommerce-activity-db` | `5440` | `ecommerce_activity_db` | `postgres_activity_data` | audit entries, notifications |

Every database that publishes or consumes messages also holds MassTransit's `InboxState`,
`OutboxMessage` and `OutboxState` tables. Every column of every table is in the generated
[reference/data-model.md](../reference/data-model.md).

Product **images** are not in any database: Catalog keeps them on the `catalog_images` volume
(specs/019), which only the container path mounts - under `start-dev` they go to the directory named by
`ProductImages:Root`.

---

## 2. Docker Compose Configuration

We use [`docker-compose.yml`](../../server/docker-compose.yml) to run every database container, RabbitMQ, Seq and pgAdmin. Every database container has a `pg_isready` health check. The excerpt below shows the pattern; the file itself is the full list:

```yaml
services:
  postgres-identity:
    image: postgres:16-alpine
    container_name: ecommerce-identity-db
    environment:
      - POSTGRES_USER=${DB_USER}
      - POSTGRES_PASSWORD=${DB_PASSWORD}
      - POSTGRES_DB=ecommerce_identity_db
    ports:
      - "5435:5432"   # not 5432: a locally installed PostgreSQL usually holds that

  postgres-catalog:
    image: postgres:16-alpine
    container_name: ecommerce-catalog-db
    environment:
      - POSTGRES_USER=${DB_USER}
      - POSTGRES_PASSWORD=${DB_PASSWORD}
      - POSTGRES_DB=ecommerce_catalog_db
    ports:
      - "5433:5432"

  postgres-order:
    image: postgres:16-alpine
    container_name: ecommerce-order-db
    environment:
      - POSTGRES_USER=${DB_USER}
      - POSTGRES_PASSWORD=${DB_PASSWORD}
      - POSTGRES_DB=ecommerce_order_db
    ports:
      - "5434:5432"

  postgres-orchestrator:
    image: postgres:16-alpine
    container_name: ecommerce-orchestrator-db
    environment:
      - POSTGRES_USER=${DB_USER}
      - POSTGRES_PASSWORD=${DB_PASSWORD}
      - POSTGRES_DB=ecommerce_saga_db
    ports:
      - "5436:5432"
```

---

## 3. Running Databases (Docker)

Open a terminal at the solution root (`server/`) and run:

```bash
# Start all containers in background
docker compose up -d

# Check service status
docker compose ps
```

> **This starts the databases only.** Since 2026-09-17 the services can also run in containers; see
> [Running in Containers](./running-in-containers.md). `docker compose up -d` on its own is
> unchanged and is still what `start-dev.sh` expects.

### Ports: host vs container

The `*_DB_PORT` values in `.env` — 5433 to 5440 — are **host publications**. Inside the container
network every PostgreSQL listens on **5432**, so the compose overlay sets each service's
`*_DB_PORT` to `5432`.

Getting this wrong produces a connection timeout that looks exactly like a dead database. It is the
single most common mistake when moving a service into a container here.

---

## 4. EF Core Migrations CLI Reference

Always execute `dotnet ef` commands from the **`server/`** directory.

> **In the container path, nobody runs these.** Each service applies its own migrations at startup
> because the overlay sets `RUN_MIGRATIONS_ON_STARTUP=true`. That setting is **off everywhere else**
> on purpose — a service that migrates on every start needs permission to alter its own schema
> forever. A runtime image has neither the SDK nor the source, so `dotnet ef` cannot run inside one,
> which is why the setting had to exist. Before 2026-09-17 nothing called `Database.Migrate()` at
> all, so a container stack would have come up healthy and empty.
>
> On the host path, `start-dev.sh` / `start-dev.ps1` run `dotnet ef database update` for all eight
> databases before launching anything.

### Migrations at startup, and the order they run in

With `RUN_MIGRATIONS_ON_STARTUP=true`, each service's `Program.cs` calls `Database.MigrateAsync()`
on its own `DbContext` after `builder.Build()` and before it serves traffic. All eight services do.

**Identity also seeds at startup** (`DataInitializer`: the four roles and, while no administrator
exists, the first one from `ADMIN_EMAIL` / `ADMIN_PASSWORD`), and seeding reads the `roles` table. Until
[#100](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/100) the seeding ran
**before** the migration block, so on an empty database it failed with
`relation "roles" does not exist`, the container exited and restarted, and it never reached the
migration that would have created the table. It went unnoticed because every container start until
then found a database that already had its schema; it surfaced when a stack was wiped and reseeded.
The migration block now runs first. No other service seeds at startup, so only Identity was affected.
See [troubleshooting §10.1](../guides/troubleshooting.md#101-a-fresh-identity-container-crash-loops-with-relation-roles-does-not-exist).

### Starting from empty

Because migrations run at startup in the container path, resetting every database is: stop the
stack, remove the volumes, start it again. The steps are in
[Getting Started](../guides/getting-started.md#seeding-a-demo-dataset).

### 4.1 Identity Microservice Migrations
```bash
dotnet ef migrations add <MigrationName> --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/
dotnet ef database update --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/
```

### 4.2 Catalog Microservice Migrations
```bash
dotnet ef migrations add <MigrationName> --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/
dotnet ef database update --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/
```

### 4.3 Order Microservice Migrations
```bash
dotnet ef migrations add <MigrationName> --project src/Services/Order/Ecommerce.Order.Infrastructure/ --startup-project src/Services/Order/Ecommerce.Order.WebApi/
dotnet ef database update --project src/Services/Order/Ecommerce.Order.Infrastructure/ --startup-project src/Services/Order/Ecommerce.Order.WebApi/
```

### 4.4 Inventory Microservice Migrations
```bash
dotnet ef migrations add <MigrationName> --project src/Services/Inventory/Ecommerce.Inventory.Infrastructure/ --startup-project src/Services/Inventory/Ecommerce.Inventory.WebApi/
dotnet ef database update --project src/Services/Inventory/Ecommerce.Inventory.Infrastructure/ --startup-project src/Services/Inventory/Ecommerce.Inventory.WebApi/
```

### 4.5 Payment Microservice Migrations
```bash
dotnet ef migrations add <MigrationName> --project src/Services/Payment/Ecommerce.Payment.Infrastructure/ --startup-project src/Services/Payment/Ecommerce.Payment.WebApi/
dotnet ef database update --project src/Services/Payment/Ecommerce.Payment.Infrastructure/ --startup-project src/Services/Payment/Ecommerce.Payment.WebApi/
```

### 4.6 Saga Orchestrator Microservice Migrations
```bash
dotnet ef migrations add <MigrationName> --project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/ --startup-project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/
dotnet ef database update --project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/ --startup-project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/
```

### 4.7 Cart Microservice Migrations
```bash
dotnet ef migrations add <MigrationName> --project src/Services/Cart/Ecommerce.Cart.Infrastructure/ --startup-project src/Services/Cart/Ecommerce.Cart.WebApi/
dotnet ef database update --project src/Services/Cart/Ecommerce.Cart.Infrastructure/ --startup-project src/Services/Cart/Ecommerce.Cart.WebApi/
```

### 4.8 Activity Microservice Migrations
```bash
dotnet ef migrations add <MigrationName> --project src/Services/Activity/Ecommerce.Activity.Infrastructure/ --startup-project src/Services/Activity/Ecommerce.Activity.WebApi/
dotnet ef database update --project src/Services/Activity/Ecommerce.Activity.Infrastructure/ --startup-project src/Services/Activity/Ecommerce.Activity.WebApi/
```

### A migration must not strand an older image

Dropping, renaming or narrowing a column means that redeploying a previous version takes the service
down rather than restoring it. Split such a change into expand, then contract; the rule is in the
[constitution](../../.specify/memory/constitution.md), and CI's `schema-compatibility` job comments on
any pull request that adds a migration doing one of those, without blocking it. The same reasoning is
why new states are often stored as columns rather than new enum values (a parcel's `DeliveredAt`, not
a `Delivered` status): an older image cannot parse a value it has never seen.

---

## 5. Accessing pgAdmin (GUI Manager)

1. Open your browser and navigate to `http://localhost:5050`.
2. Log in with `PGADMIN_EMAIL` / `PGADMIN_PASSWORD` from `server/.env`.
3. Add servers (Connect using Container Name and internal port `5432`):
   - **Identity DB Connection**: Host `ecommerce-identity-db`, Port `5432`, DB `ecommerce_identity_db`

   > Those are the ports *inside* the Docker network, which is why they are all `5432` regardless of
   > what each container publishes on the host. pgAdmin runs as a container and reaches the databases
   > by service name. To point it at a PostgreSQL installed on your own machine instead, use host
   > `host.docker.internal`.
   - **Catalog DB Connection**: Host `ecommerce-catalog-db`, Port `5432`, DB `ecommerce_catalog_db`
   - **Order DB Connection**: Host `ecommerce-order-db`, Port `5432`, DB `ecommerce_order_db`
   - **Orchestrator Saga DB Connection**: Host `ecommerce-orchestrator-db`, Port `5432`, DB `ecommerce_saga_db`
   - **Inventory DB Connection**: Host `ecommerce-inventory-db`, Port `5432`, DB `ecommerce_inventory_db`
   - **Payment DB Connection**: Host `ecommerce-payment-db`, Port `5432`, DB `ecommerce_payment_db`
   - **Cart DB Connection**: Host `ecommerce-cart-db`, Port `5432`, DB `ecommerce_cart_db`
   - **Activity DB Connection**: Host `ecommerce-activity-db`, Port `5432`, DB `ecommerce_activity_db`
