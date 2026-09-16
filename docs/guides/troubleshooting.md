# Troubleshooting & Common Compile Errors

This guide documents common compile-time errors encountered during C# and .NET multi-project development (such as Clean Architecture) and how to diagnose and resolve them.

---

## 1. Missing Project Reference (CS0234 / CS0246)

### Symptoms
Triggers when compiling or referencing types from another layer/project. The compiler complains about missing namespaces:
```text
The type or namespace name 'Domain' does not exist in the namespace 'Ecommerce' (are you missing an assembly reference?) [CS0234]
```

### Diagnosis
In .NET, projects (class libraries) are isolated by default. A project cannot access types from another project unless it explicitly references it in its `.csproj` file.

Open the `.csproj` file of the project reporting the error (e.g., `Ecommerce.Infrastructure.csproj`) and look for the `<ProjectReference>` tag.

### Solution
Run the following dotnet CLI command from the solution root:
```bash
dotnet add <project_reporting_error> reference <project_containing_types>
```

#### Example:
To allow `Infrastructure` to access `Application`:
```bash
dotnet add src/Services/Identity/Ecommerce.Identity.Infrastructure/Ecommerce.Identity.Infrastructure.csproj reference src/Services/Identity/Ecommerce.Identity.Application/Ecommerce.Identity.Application.csproj
```

---

## 2. Missing NuGet Package for Extension Methods (CS1503 / CS1061)

### Symptoms
Triggers when calling common extension methods (e.g., `.Configure<TOptions>()` or `.UseNpgsql()`) with the correct parameters, but the compiler reports either a missing method or a type-conversion error:
```text
cannot convert from 'Microsoft.Extensions.Configuration.IConfigurationSection' to 'System.Action<Ecommerce.Infrastructure.Security.JwtSettings>' [CS1503]
```

### Diagnosis
In .NET, many common methods are **Extension Methods** defined in separate NuGet packages. If the required package is not installed:
1. The compiler cannot find the specific overload.
2. It tries to match your call against a default fallback overload (e.g., one taking an `Action<T>` delegate).
3. This triggers a mismatch parameter/type conversion error (`CS1503`).

### Common Extensions and Required Packages

| Extension Method | Purpose | Required NuGet Package |
| :--- | :--- | :--- |
| `services.Configure<T>(IConfiguration)` | Binding configs to type-safe settings classes | `Microsoft.Extensions.Options.ConfigurationExtensions` |
| `options.UseNpgsql(connectionString)` | Configuring PostgreSQL DbContext provider | `Npgsql.EntityFrameworkCore.PostgreSQL` |
| EF Migrations commands | Running migrations and database updates | `Microsoft.EntityFrameworkCore.Design` |

### Solution
Install the missing package into the project reporting the error:
```bash
dotnet add <project_path> package <package_name>
```

#### Example:
To fix the options binding error in `Infrastructure`:
```bash
dotnet add src/Services/Identity/Ecommerce.Identity.Infrastructure/Ecommerce.Identity.Infrastructure.csproj package Microsoft.Extensions.Options.ConfigurationExtensions
```

---

## 3. `.env` File Path Mismatch during Startup

### Symptoms
Environment variables defined in `.env` are not loaded when running the Web API, causing runtime configuration errors:
```text
System.ArgumentException: IDX10703: Cannot create a 'Microsoft.IdentityModel.Tokens.SymmetricSecurityKey', key length is zero.
```

### Diagnosis
When running the Web API via `dotnet run --project src/Services/Identity/Ecommerce.Identity.WebApi/`, the working directory (`Directory.GetCurrentDirectory()`) defaults to the project folder (`server/src/Services/Identity/Ecommerce.Identity.WebApi`).
However, the `.env` file is located at the root of the backend folder (`server/`).
If you try to load it using `Path.Combine(Directory.GetCurrentDirectory(), ".env")`, the file path evaluates to `server/src/Services/Identity/Ecommerce.Identity.WebApi/.env` which does not exist, causing the loading logic to fail silently.

### Solution
Implement a recursive upward directory search in `Program.cs` to locate the `.env` file starting from the current directory:

```csharp
var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
string? dotenv = null;
while (directory != null)
{
    var path = Path.Combine(directory.FullName, ".env");
    if (File.Exists(path))
    {
        dotenv = path;
        break;
    }
    directory = directory.Parent; // Move up one level
}

if (!string.IsNullOrEmpty(dotenv))
{
    // Parse and set variables
}
```

---

## 4. EF Core `DbUpdateConcurrencyException` on Nested Inserts

### Symptoms
When adding a new entity to a collection navigation property of an existing tracked entity (e.g., `user.RefreshTokens.Add(newToken)`) and calling `SaveChangesAsync`, EF Core throws:
```text
Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException: The database operation was expected to affect 1 row(s), but actually affected 0 row(s).
```

### Diagnosis
EF Core determines whether a tracked entity is new (`Added` state) or existing (`Modified` state) based on its primary key value.
If you declare an inline initializer for Guid keys in your domain models (e.g., `public Guid Id { get; set; } = Guid.NewGuid();`), the key is immediately populated with a non-default value when instantiated in memory.
When this new entity is added to a tracked parent's collection without explicitly calling `DbSet.Add()`, EF Core checks the key:
1. Because `Id != Guid.Empty`, EF Core assumes this is an existing database record.
2. It marks the entity state as `Modified` and generates an `UPDATE` statement.
3. Since this Guid only exists in RAM and not in the database, the `UPDATE` affects 0 rows, triggering the concurrency exception.

### Solution
Remove inline default initializers (`= Guid.NewGuid()`) from primary key properties of your domain entities. Let them default to `Guid.Empty` so EF Core's Change Tracker can correctly infer that they are new and mark them as `Added` (generating an `INSERT` statement).

```csharp
// BAD - Confuses EF Core Change Tracker on navigation inserts
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

// GOOD - Defaults to Guid.Empty, letting EF Core detect it as new
public class RefreshToken
{
    public Guid Id { get; set; }
}
```

---

## 5. MediatR / MassTransit Version Mismatch & Commercial License Prompt

### Symptoms
During runtime API invocation (such as `POST /api/categories`) or service startup:
1. `System.MissingMethodException: Method not found: 'System.Threading.Tasks.Task`1<System.__Canon> MediatR.RequestHandlerDelegate`1.Invoke()'` inside `ValidationBehavior`.
2. Warning from `LuckyPennySoftware.MediatR.License`: *"You do not have a valid license key for the Lucky Penny software MediatR."*
3. `MassTransit.ConfigurationException`: *"License must be specified with SetLicense/SetLicenseLocation"*.

### Diagnosis
1. **Commercial License Forks**: Recent releases of MediatR (`14.x`) and MassTransit (`9.x`) introduced commercial/community license checks or forks (`LuckyPennySoftware`), breaking open-source expectations.
2. **Runtime Delegate Mismatch**: If a shared building block (`Ecommerce.Shared`) was compiled against official open-source MediatR `12.4.1`, while an application project (`Ecommerce.Catalog.Application`) transitively imported MediatR `14.x`, the signature of `RequestHandlerDelegate<TResponse>` differs between assemblies. When `ValidationBehavior` attempts to call `next()`, the runtime throws `MissingMethodException`.

### Solution
Standardize all projects across the monorepo on the official, 100% free open-source LTS versions:

1. **MediatR**: Standardize on **`12.4.1`** (Official MIT open-source release by Jimmy Bogard).
2. **MassTransit**: Standardize on **`8.3.6`** (Official Apache 2.0 open-source LTS release).

Run the following commands across affected projects:
```bash
# Standardize MediatR to 12.4.1
dotnet add src/BuildingBlocks/Ecommerce.Shared/ package MediatR --version 12.4.1
dotnet add src/Services/Catalog/Ecommerce.Catalog.Application/ package MediatR --version 12.4.1
dotnet add src/Services/Identity/Ecommerce.Identity.Application/ package MediatR --version 12.4.1

# Standardize MassTransit to 8.3.6
dotnet add src/Services/Catalog/Ecommerce.Catalog.Application/ package MassTransit.Abstractions --version 8.3.6
dotnet add src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ package MassTransit.EntityFrameworkCore --version 8.3.6
dotnet add src/Services/Catalog/Ecommerce.Catalog.WebApi/ package MassTransit.RabbitMQ --version 8.3.6
```

---

## 6. A Local PostgreSQL Install Shadowing the Docker Container

### Symptoms

* `dotnet ef database update` reports **"No migrations were applied. The database is already up to date"**, yet querying the container shows nothing:
  ```
  ERROR:  relation "roles" does not exist
  ```
* Data written through the API is invisible in pgAdmin when connected to the container.
* Only the **Identity** service is affected. Catalog, Order and Orchestrator behave normally.

### Diagnosis

A PostgreSQL instance installed directly on the machine (Windows service `postgresql-x64-16`, or
Homebrew/apt elsewhere) already listens on port `5432` — the same port `ecommerce-identity-db` is
published on. Docker may still bind successfully on a different interface (IPv6 `::` versus IPv4
`0.0.0.0`), so `docker compose up` reports no conflict, but `Host=localhost` resolves to the
**native** instance first.

The result is a split brain: Identity reads and writes the local PostgreSQL install, while every
other service uses its container. Only Identity is hit because the others publish `5433`, `5434` and
`5436`, which nothing else claims.

Confirm what actually holds the port:

```powershell
Get-NetTCPConnection -LocalPort 5432 -State Listen |
  Select-Object LocalAddress, OwningProcess,
    @{n='Process';e={(Get-Process -Id $_.OwningProcess).ProcessName}}
```

Two rows — one `com.docker.backend`, one `postgres` — confirms the clash.

```bash
# Linux / macOS
sudo lsof -iTCP:5432 -sTCP:LISTEN
```

### Solution

**Option A — free the port** (simplest if the local install is unused):

```powershell
Stop-Service postgresql-x64-16
Set-Service postgresql-x64-16 -StartupType Manual   # keep it from coming back on reboot
```

**Option B — move Identity to a free port** (keep both):

1. Change the published port in `docker-compose.yml` to `5435:5432`.
2. Add `IDENTITY_DB_PORT=5435` to `server/.env`.
3. Recreate the container and re-run the migration:
   ```bash
   docker compose up -d --force-recreate postgres-identity
   dotnet ef database update --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/
   ```

Either way the container database starts empty, so the migration runs from scratch and the startup
initializer re-seeds the roles and bootstrap administrator. Any data that was sitting in the native
instance stays there — export it first if you need it.

---

## 7. General Diagnosis Checklist

If your IDE reports red errors but your code looks correct:

1. **Verify via CLI**: Open the terminal and run a manual build. IDE caches can sometimes be stale:
   ```bash
   dotnet build Ecommerce.slnx
   ```
2. **Inspect `.csproj` Files**: Treat `.csproj` files as the source-of-truth configuration for dependencies. Ensure both `<ProjectReference>` (other projects) and `<PackageReference>` (NuGet packages) are correct.
3. **Check Namespaces**: Ensure the files have the correct `using` statements at the top. Extension methods often require importing the core namespace (e.g., `using Microsoft.EntityFrameworkCore;`).
4. **Confirm which database you are actually talking to**: when data "disappears", check the port for a second PostgreSQL instance before suspecting the code (see section 6).
