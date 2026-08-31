# Database Setup & Migrations (PostgreSQL & Docker)

This guide explains how to set up, run, and manage a local PostgreSQL database using Docker Compose and Entity Framework Core (EF Core) Migrations.

---

## 1. Prerequisites

Before starting, ensure you have:
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running.
- [.NET SDK](https://dotnet.microsoft.com/download) installed.

---

## 2. Docker Compose Configuration

We use `docker-compose.yml` to orchestrate our database and database management tool (pgAdmin).

The file is located in the solution root directory: [docker-compose.yml](file:///d:/Code/CSharp/e-commerce/server/docker-compose.yml).

### Services Configured

1. **PostgreSQL**:
   - **Image**: `postgres:16-alpine` (lightweight and secure version).
   - **Container Name**: `e-commerce-db`
   - **Default Database**: `e-commerce` (set via `.env` file).
   - **Port**: `5432` mapped to host `5432`.
   - **Data Volume**: Persistent volume named `postgres_data` mapped to `/var/lib/postgresql/data`.

2. **pgAdmin**:
   - **Image**: `dpage/pgadmin4` (GUI manager).
   - **Container Name**: `habit-tracker-pgadmin`
   - **Port**: `5050` mapped to host `80`.
   - **Default Credentials**: 
     - Email: `admin@admin.com`
     - Password: `admin`

---

## 3. Running the Database (Docker)

Open a terminal at the solution root (`server/`) and run:

### Start Services (in Detached Mode)
```bash
docker compose up -d
```

### Check Service Status
```bash
docker compose ps
```

### Stop Services
```bash
docker compose down
```

---

## 4. Connecting to the Database

### Connection String
To connect your ASP.NET Core application to this database, use the following connection string in your configuration ([appsettings.json](file:///d:/Code/CSharp/e-commerce/server/src/Ecommerce.WebApi/appsettings.json)):

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=e-commerce;Username=postgres;Password=123456;Port=5432"
}
```

### Registering PostgreSQL in ASP.NET Core
In the **Infrastructure** project, we configure the DbContext connection:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

---

## 5. Entity Framework Core Migrations Guide

Entity Framework Core (EF Core) Migrations allow us to keep our PostgreSQL database schema in sync with our C# model entities (Domain).

### 5.1 Package Overview

| Package Name | Target Project | Purpose |
| :--- | :--- | :--- |
| **`Microsoft.EntityFrameworkCore`** | Application / Infrastructure | Core ORM logic, base DbContext class, and DbSet definitions. |
| **`Npgsql.EntityFrameworkCore.PostgreSQL`** | Infrastructure | PostgreSQL database provider for EF Core (translates C# LINQ queries to Postgres SQL). |
| **`Microsoft.EntityFrameworkCore.Design`** | WebApi | Required for EF Core CLI tools to execute migrations at compile-time (installed in the Startup project). |

### 5.2 EF Core CLI Tool Installation

To run migration commands, you need to install the `dotnet ef` tool globally on your machine:

```bash
dotnet tool install --global dotnet-ef
```

*To update the tool to the latest version:*
```bash
dotnet tool update --global dotnet-ef
```

### 5.3 Command Reference

Always run these commands from the **`server/`** directory where the solution file resides.

#### 1. Add a New Migration
Compares your current C# models with the previous migration snapshot and generates C# script files for the changes:
```bash
dotnet ef migrations add <MigrationName> --project src/Ecommerce.Infrastructure/ --startup-project src/Ecommerce.WebApi/
```
* **`--project`**: Path to the project containing the `DbContext` and configurations (`Infrastructure` layer).
* **`--startup-project`**: Path to the entry-point project containing the configurations/connection string (`WebApi` layer).

#### 2. Apply Migrations to Database (Update DB)
Runs all pending migrations on your active PostgreSQL database:
```bash
dotnet ef database update --project src/Ecommerce.Infrastructure/ --startup-project src/Ecommerce.WebApi/
```

#### 3. Remove Latest Migration
Deletes the latest migration files (only if it has **not** been applied to the database yet, or after rolling back):
```bash
dotnet ef migrations remove --project src/Ecommerce.Infrastructure/ --startup-project src/Ecommerce.WebApi/
```

---

## 6. Accessing pgAdmin (Optional)

1. Open your browser and navigate to `http://localhost:5050`.
2. Log in with the credentials:
   - **Email**: `admin@admin.com`
   - **Password**: `admin`
3. Click "Add New Server" to connect to PostgreSQL:
   - **General Tab**: 
     - Name: `E-commerce DB`
   - **Connection Tab**:
     - Host name/address: `postgres` (the service name in docker-compose)
     - Port: `5432`
     - Maintenance database: `e-commerce`
     - Username: `postgres`
     - Password: `your_secure_password_from_env`
4. Click Save. You can now inspect tables and run SQL queries.
