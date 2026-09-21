# Database Setup & Migrations (Database per Service)

This guide explains how to set up, run, and manage isolated PostgreSQL databases using Docker Compose and Entity Framework Core (EF Core) Migrations in our Monorepo Microservices Architecture.

---

## 1. Database per Service Isolation

Each microservice in our architecture owns a dedicated, isolated PostgreSQL database:

| Microservice | Container Name | Port | Database Name | Primary Purpose |
| :--- | :--- | :--- | :--- | :--- |
| **Identity Service** | `ecommerce-identity-db` | `5435` | `ecommerce_identity_db` | Users, Roles, Refresh Tokens |
| **Catalog Service** | `ecommerce-catalog-db` | `5433` | `ecommerce_catalog_db` | Categories, Products, Outbox Messages |
| **Order Service** | `ecommerce-order-db` | `5434` | `ecommerce_order_db` | Orders, Order Items, Outbox Messages |
| **Orchestrator Service** | `ecommerce-orchestrator-db` | `5436` | `ecommerce_saga_db` | Order Saga State Machine Persistence |
| **Inventory Service** | `ecommerce-inventory-db` | `5437` | `ecommerce_inventory_db` | Stock Items, Reservations, Outbox and Inbox |
| **Payment Service** | `ecommerce-payment-db` | `5438` | `ecommerce_payment_db` | Payments, Outbox and Inbox |
| **Cart Service** | `ecommerce-cart-db` | `5439` | `ecommerce_cart_db` | Carts, Cart Lines, Checkout Outcomes |

---

## 2. Docker Compose Configuration

We use [`docker-compose.yml`](../../server/docker-compose.yml) to run every database container, RabbitMQ and pgAdmin. The excerpt below shows the pattern; the file itself is the full list:

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

The `*_DB_PORT` values in `.env` — 5433 to 5439 — are **host publications**. Inside the container
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
