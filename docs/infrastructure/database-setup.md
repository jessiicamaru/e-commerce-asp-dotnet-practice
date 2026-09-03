# Database Setup & Migrations (Database per Service)

This guide explains how to set up, run, and manage isolated PostgreSQL databases using Docker Compose and Entity Framework Core (EF Core) Migrations in our Monorepo Microservices Architecture.

---

## 1. Database per Service Isolation

Each microservice in our architecture owns a dedicated, isolated PostgreSQL database:

| Microservice | Container Name | Port | Database Name | Primary Purpose |
| :--- | :--- | :--- | :--- | :--- |
| **Identity Service** | `ecommerce-identity-db` | `5432` | `ecommerce_identity_db` | Users, Roles, Refresh Tokens |
| **Catalog Service** | `ecommerce-catalog-db` | `5433` | `ecommerce_catalog_db` | Categories, Products, Outbox Messages |
| **Order Service** | `ecommerce-order-db` | `5434` | `ecommerce_order_db` | Orders, Order Items, Outbox Messages |
| **Orchestrator Service** | `ecommerce-orchestrator-db` | `5436` | `ecommerce_saga_db` | Order Saga State Machine Persistence |

---

## 2. Docker Compose Configuration

We use [`docker-compose.yml`](file:///d:/Code/CSharp/e-commerce/server/docker-compose.yml) to orchestrate all four database containers, RabbitMQ, and pgAdmin:

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
      - "5432:5432"

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

---

## 4. EF Core Migrations CLI Reference

Always execute `dotnet ef` commands from the **`server/`** directory.

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

### 4.4 Saga Orchestrator Microservice Migrations
```bash
dotnet ef migrations add <MigrationName> --project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/ --startup-project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/
dotnet ef database update --project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/ --startup-project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/
```

---

## 5. Accessing pgAdmin (GUI Manager)

1. Open your browser and navigate to `http://localhost:5050`.
2. Log in with credentials: `admin@admin.com` / `123456`.
3. Add servers (Connect using Container Name and internal port `5432`):
   - **Identity DB Connection**: Host `ecommerce-identity-db`, Port `5432`, DB `ecommerce_identity_db`
   - **Catalog DB Connection**: Host `ecommerce-catalog-db`, Port `5432`, DB `ecommerce_catalog_db`
   - **Order DB Connection**: Host `ecommerce-order-db`, Port `5432`, DB `ecommerce_order_db`
   - **Orchestrator Saga DB Connection**: Host `ecommerce-orchestrator-db`, Port `5432`, DB `ecommerce_saga_db`
