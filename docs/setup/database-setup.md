# Database Setup with PostgreSQL & Docker

This guide explains how to set up and run a local PostgreSQL database using Docker Compose for the application.

---

## 1. Prerequisites

Before starting, ensure you have:
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running.
- Basic understanding of Docker commands.

---

## 2. Docker Compose Configuration

We use `docker-compose.yml` to orchestrate our database and database management tool (pgAdmin).

The file is located in the solution root directory: [docker-compose.yml](file:///d:/Code/CSharp/e-commerce/server/docker-compose.yml).

### Services Configured

1. **PostgreSQL**:
   - **Image**: `postgres:16-alpine` (lightweight and secure version).
   - **Container Name**: `habit-tracker-db`
   - **Default Database**: `habit-tracker` (as mandated by project conventions).
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

## 3. Running the Database

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
To connect your ASP.NET Core application to this database, use the following connection string in your configuration:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=habit-tracker;Username=postgres;Password=your_secure_password;Port=5432"
}
```

### Registering PostgreSQL in ASP.NET Core

To use PostgreSQL in your .NET projects, you need to install the Entity Framework Core provider for PostgreSQL in your **Infrastructure** project:

```bash
dotnet add src/Ecommerce.Infrastructure/Ecommerce.Infrastructure.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
```

Then register the DbContext in your dependency injection container:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

---

## 5. Accessing pgAdmin (Optional)

1. Open your browser and navigate to `http://localhost:5050`.
2. Log in with the credentials:
   - **Email**: `admin@admin.com`
   - **Password**: `admin`
3. Click "Add New Server" to connect to PostgreSQL:
   - **General Tab**: 
     - Name: `Habit Tracker DB`
   - **Connection Tab**:
     - Host name/address: `postgres` (the service name in docker-compose)
     - Port: `5432`
     - Maintenance database: `habit-tracker`
     - Username: `postgres`
     - Password: `your_secure_password`
4. Click Save. You can now inspect tables and run SQL queries.
