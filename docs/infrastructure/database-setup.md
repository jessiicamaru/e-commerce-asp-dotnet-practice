# Database Setup & Migrations (Database per Service)

This guide explains how to set up, run, and manage isolated PostgreSQL databases using Docker Compose and Entity Framework Core (EF Core) Migrations in our Monorepo Microservices Architecture.

---

## 1. Database per Service Isolation

Each microservice in our architecture owns a dedicated, isolated PostgreSQL database:

| Microservice | Container Name | Port | Database Name | Primary Purpose |
| :--- | :--- | :--- | :--- | :--- |
| **Identity Service** | `ecommerce-identity-db` | `5432` | `ecommerce_identity_db` | Users, Roles, Refresh Tokens |
| **Catalog Service** | `ecommerce-catalog-db` | `5433` | `ecommerce_catalog_db` | Categories, Products |

---

## 2. Docker Compose Configuration

We use [`docker-compose.yml`](file:///d:/Code/CSharp/e-commerce/server/docker-compose.yml) to orchestrate both database containers and pgAdmin:

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
    volumes:
      - postgres_identity_data:/var/lib/postgresql/data

  postgres-catalog:
    image: postgres:16-alpine
    container_name: ecommerce-catalog-db
    environment:
      - POSTGRES_USER=${DB_USER}
      - POSTGRES_PASSWORD=${DB_PASSWORD}
      - POSTGRES_DB=ecommerce_catalog_db
    ports:
      - "5433:5432"
    volumes:
      - postgres_catalog_data:/var/lib/postgresql/data
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
# Add a new migration for Identity
dotnet ef migrations add <MigrationName> --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/

# Update Identity Database (Port 5432)
dotnet ef database update --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/
```

### 4.2 Catalog Microservice Migrations
```bash
# Add a new migration for Catalog
dotnet ef migrations add <MigrationName> --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/

# Update Catalog Database (Port 5433)
dotnet ef database update --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/
```

---

## 5. Accessing pgAdmin (GUI Manager)

1. Open your browser and navigate to `http://localhost:5050`.
2. Log in with credentials: `admin@admin.com` / `admin`.
3. Add servers:
   - **Identity DB Connection**:
     - Host: `postgres-identity` (Port `5432`)
     - Database: `ecommerce_identity_db`
   - **Catalog DB Connection**:
     - Host: `postgres-catalog` (Port `5432` internal)
     - Database: `ecommerce_catalog_db`
