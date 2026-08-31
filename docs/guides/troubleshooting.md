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
dotnet add src/Ecommerce.Infrastructure/Ecommerce.Infrastructure.csproj reference src/Ecommerce.Application/Ecommerce.Application.csproj
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
dotnet add src/Ecommerce.Infrastructure/Ecommerce.Infrastructure.csproj package Microsoft.Extensions.Options.ConfigurationExtensions
```

---

## 3. `.env` File Path Mismatch during Startup

### Symptoms
Environment variables defined in `.env` are not loaded when running the Web API, causing runtime configuration errors:
```text
System.ArgumentException: IDX10703: Cannot create a 'Microsoft.IdentityModel.Tokens.SymmetricSecurityKey', key length is zero.
```

### Diagnosis
When running the Web API via `dotnet run --project src/Ecommerce.WebApi/`, the working directory (`Directory.GetCurrentDirectory()`) defaults to the project folder (`server/src/Ecommerce.WebApi`).
However, the `.env` file is located at the root of the backend folder (`server/`).
If you try to load it using `Path.Combine(Directory.GetCurrentDirectory(), ".env")`, the file path evaluates to `server/src/Ecommerce.WebApi/.env` which does not exist, causing the loading logic to fail silently.

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

## 5. General Diagnosis Checklist

If your IDE reports red errors but your code looks correct:

1. **Verify via CLI**: Open the terminal and run a manual build. IDE caches can sometimes be stale:
   ```bash
   dotnet build Ecommerce.slnx
   ```
2. **Inspect `.csproj` Files**: Treat `.csproj` files as the source-of-truth configuration for dependencies. Ensure both `<ProjectReference>` (other projects) and `<PackageReference>` (NuGet packages) are correct.
3. **Check Namespaces**: Ensure the files have the correct `using` statements at the top. Extension methods often require importing the core namespace (e.g., `using Microsoft.EntityFrameworkCore;`).
