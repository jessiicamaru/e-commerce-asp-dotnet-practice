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

## 3. General Diagnosis Checklist

If your IDE reports red errors but your code looks correct:

1. **Verify via CLI**: Open the terminal and run a manual build. IDE caches can sometimes be stale:
   ```bash
   dotnet build Ecommerce.slnx
   ```
2. **Inspect `.csproj` Files**: Treat `.csproj` files as the source-of-truth configuration for dependencies. Ensure both `<ProjectReference>` (other projects) and `<PackageReference>` (NuGet packages) are correct.
3. **Check Namespaces**: Ensure the files have the correct `using` statements at the top. Extension methods often require importing the core namespace (e.g. `using Microsoft.EntityFrameworkCore;`).
