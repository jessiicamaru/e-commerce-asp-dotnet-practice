# Troubleshooting & Common Compile Errors

This guide documents common compile-time errors encountered during C# and .NET multi-project development (such as Clean Architecture) and how to diagnose and resolve them, and - from §6 on - the runtime, container and test traps this codebase has actually sprung, each with its symptom, cause and fix.

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
The root cause is a convention: EF Core treats a `Guid` key as **generated on add**, so a key that
already has a value must belong to a row that exists. There are two ways out, and this project uses
the first:

1. **Keep generating the id in the application — as [ADR-001](../architecture/adr-001-uuidv7-primary-keys.md)
   requires (`Guid.CreateVersion7()`) — and tell EF so:**

   ```csharp
   builder.Property(l => l.Id).ValueGeneratedNever();
   ```

   EF then treats an entity found through a navigation as new, and issues an `INSERT`. This is what
   Cart does for `Cart.Id` and `CartLine.Id`; before it did, all eight of its consumer tests failed
   with exactly this exception. It changes the model, not the schema — no migration.
2. **Leave the key empty** (`Guid.Empty`) and let EF generate it. This works, but EF's generated
   values are **not UUID v7**, so it contradicts ADR-001. Identity's `RefreshToken` and self-registered
   users work this way today.

Adding the child through its own `DbSet.Add(...)` instead of the parent's collection also avoids the
exception, but is easy to forget at the next call site.

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

## 6. A Locally Installed Service Shadowing a Docker Container

### Symptoms

* `dotnet ef database update` reports **"No migrations were applied. The database is already up to
  date"**, yet querying the container shows nothing:
  ```
  ERROR:  relation "roles" does not exist
  ```
* Data written through the API is invisible in pgAdmin when connected to the container.
* CI fails where your machine passes — for example a service never reaching a healthy state because
  no broker is reachable, while locally it starts fine.

### Diagnosis

A service installed directly on the machine — PostgreSQL on `5432`, RabbitMQ on `5672` — already
holds the port a container publishes on. Docker may still bind successfully on a different
interface (IPv6 `::` versus IPv4 `0.0.0.0`), so `docker compose up` reports no conflict, but
`Host=localhost` resolves to the **native** instance first.

The result is a split brain: the application talks to the local install while the container sits
unused, and the difference only surfaces somewhere without that local install, such as CI.

```powershell
# Windows — anything listed besides com.docker.backend is shadowing the container
Get-NetTCPConnection -LocalPort 5432, 5672 -State Listen |
  Select-Object LocalPort, LocalAddress,
    @{n='Process';e={(Get-Process -Id $_.OwningProcess).ProcessName}}
```

```bash
# Linux / macOS
sudo lsof -iTCP:5432 -sTCP:LISTEN
sudo lsof -iTCP:5672 -sTCP:LISTEN
```

An `erl` process on `5672` is a native RabbitMQ; a `postgres` process on `5432` is a native
PostgreSQL.

### Solution

**PostgreSQL — already handled.** The Identity database publishes on **`5435`**, not `5432`, so it
no longer collides with a local PostgreSQL install. `server/.env` sets `IDENTITY_DB_PORT=5435` to
match. Keep those two in sync if you change either. The other databases (`5433`, `5434`, `5436`–`5440`)
never collided.

**RabbitMQ — stop the local install.** The services call `cfg.Host(rabbitHost, "/", ...)` and pass
no port, so the broker must be on the default `5672`; you cannot move the container out of the way
without a code change. Free the port instead:

```powershell
Stop-Service RabbitMQ
Set-Service RabbitMQ -StartupType Manual   # keep it from returning on reboot
```

Uninstalling the local RabbitMQ works too. Either way `docker compose up -d rabbitmq` then owns
`5672`. Queues and messages held by the local broker are lost, which is harmless here — MassTransit
recreates its exchanges and queues on startup.

### After freeing a port

The container starts empty, so re-run the migration and let the service seed itself:

```bash
docker compose up -d
dotnet ef database update --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/
```

Data left behind in the native instance stays there — dump it first if you need it.

### A worked example: your own leftover process

Section 6 is usually read as being about *software you installed*. It is not — **a `dotnet run` you
forgot about does the same thing, and is harder to suspect.**

On 2026-09-17 a Payment process left over from an earlier test, started with
`PAYMENT_OUTCOME=Reject`, sat on port 5061 while the Payment container ran behind it. Orders failed
for no visible reason. The container's environment said `Approve`, its `appsettings.json` said
`Approve`, and `curl localhost:5061/health` said `Rejected` — because the request never reached the
container. The kill command that should have stopped it earlier had been chained with `;` and failed
silently.

Before trusting any container result:

```bash
# Windows
Get-Process | Where-Object { $_.ProcessName -like 'Ecommerce.*' }
# Linux / macOS
pgrep -fa Ecommerce.
```

**This must be empty**, and *checking that it is* matters more than running the kill — a kill that
fails quietly leaves you debugging the wrong process.

---

## 7. Container-Specific Traps

Added 2026-09-17 with [Running in Containers](../infrastructure/running-in-containers.md).

### 7.1 A service cannot reach its database, and the error names `localhost`

`DB_HOST` is not reaching it. Inside a container `localhost` is *that container*.

### 7.2 A service times out reaching a database that is clearly healthy

`*_DB_PORT` is almost certainly 5433–5440. Those are **host publications**; inside the container
network every PostgreSQL listens on **5432**. This looks like a dead database and is not one.

### 7.3 Every authenticated request returns 401 across services

`JWT_SECRET` differs between containers. Identity signs with it and everyone else validates with it,
so a mismatch presents as an authorization failure rather than a configuration one. Check it is
supplied from a single source to every service.

### 7.4 A configuration override in an environment variable is ignored

Known case: the gateway's YARP destinations do **not** bind from
`ReverseProxy__Clusters__<name>__Destinations__destination1__Address`. The variable is present and
YARP still dials `localhost:5057`. On the same container `Logging__LogLevel__Default=Warning` works,
so the environment provider is fine, and the colon-separated form fails too. **Command-line
arguments bind** — that is what the compose overlay uses.

The cause is not established. If an override is being ignored, try the command-line form before
assuming your syntax is wrong.

### 7.5 `docker compose ps` says "running" for a service that is answering errors

The service has no health check, or its health check cannot run. `aspnet:10.0` ships neither `curl`
nor `wget`, and a container health check has to be a command *inside* the container — `curl` is
installed in the runtime stage for exactly this.

### 7.6 A secret scan reports an image clean when it is not

Do not check the running container:

```bash
docker run --rm <image> ls -la /app     # proves nothing
```

A `COPY . .` followed by `RUN rm .env` leaves the file fully readable in the earlier layer. Layers
are additive. Use
[`verify-image-has-no-secrets.sh`](../../.github/scripts/verify-image-has-no-secrets.sh), which
decompresses every layer and reports how many it read — a scan of zero layers fails rather than
passing. If it ever reports a leak, **rotate the credentials**; an image built once may already have
been pulled.

---

## 8. Accounts that differ only by email case

Identity refuses to start after upgrading past `CaseInsensitiveEmail` (#49), with:

```text
Cannot make emails case-insensitive: 1 email(s) belong to more than one account, differing only by case.
```

Before #49, `Someone@Example.com` and `someone@example.com` could register as **two** accounts,
each with its own password, addresses and orders. The migration adds a unique index on
`lower("Email")`, and it cannot do that while such pairs exist. It stops rather than choosing which
account survives, because that is a decision about a person's data. The message gives a count and
no addresses, because it ends up in logs.

**Find them:**

```sql
SELECT lower("Email") AS mailbox, array_agg("Email" ORDER BY "Id") AS accounts, count(*)
FROM users GROUP BY 1 HAVING count(*) > 1;
```

**Resolve each one.** Pick the account to keep (usually the one with the orders), then either:

- **delete the other** if it holds nothing worth keeping (its refresh tokens and addresses go with it), or
- **retire it** by changing its email to something that no longer collides, for example
  `UPDATE users SET "Email" = 'retired+' || "Id" || '@invalid' WHERE "Id" = '<id>';`. That keeps its
  rows but means nobody can sign in to it.

Orders live in Order's database and reference the user id, so neither option loses an order. A
retired account's orders simply stay with an account nobody can sign in to. Once the query returns
nothing, start Identity again and the migration completes.

---

## 9. General Diagnosis Checklist

If your IDE reports red errors but your code looks correct:

1. **Verify via CLI**: Open the terminal and run a manual build. IDE caches can sometimes be stale:
   ```bash
   dotnet build Ecommerce.slnx
   ```
2. **Inspect `.csproj` Files**: Treat `.csproj` files as the source-of-truth configuration for dependencies. Ensure both `<ProjectReference>` (other projects) and `<PackageReference>` (NuGet packages) are correct.
3. **Check Namespaces**: Ensure the files have the correct `using` statements at the top. Extension methods often require importing the core namespace (e.g., `using Microsoft.EntityFrameworkCore;`).
4. **Confirm which service you are actually talking to**: when data "disappears", or when CI fails on something that works locally, check whether a natively installed PostgreSQL or RabbitMQ is holding the port instead of the container (see section 6).

---

## 10. Traps found since 2026-09-22

### 10.1 A fresh Identity container crash-loops with `relation "roles" does not exist`

**Symptom.** On a stack started with empty database volumes, `docker compose ... ps` shows
`ecommerce-identity` restarting over and over, and its log ends with:

```text
Npgsql.PostgresException: 42P01: relation "roles" does not exist
Database seeding failed. Have the EF Core migrations been applied?
```

Every other service comes up; the gateway, which waits for Identity to be healthy, never does.

**Cause.** With `RUN_MIGRATIONS_ON_STARTUP=true`, Identity ran its startup seeding (`DataInitializer`:
the roles and the first administrator) **before** applying its migrations. On an empty database the
seeding read a `roles` table that did not exist yet, the process exited, and it never reached the
migration that would have created the table. Nobody saw it earlier because every container start
until then found a database that already had its schema; it surfaced when a stack was wiped and
reseeded. Identity is the only service that seeds at startup.

**Fix.** Fixed in [#100](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/100):
`Program.cs` now migrates first and seeds second. Rebuild the Identity image
(`up -d --build`). If you are running an image from before #100, apply Identity's migrations once
from the host against port `5435` and the container will start:

```bash
cd server
dotnet ef database update --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/
```

### 10.2 Bruno: a request using `{{sellerToken}}` gets 401 with a setup that looks right

**Symptom.** A new request placed in any folder other than `bruno/seller/` sends
`Authorization: Bearer {{sellerToken}}` and gets **401**, although the collection signs a seller in
and the same request works when sent by hand afterwards. Decoding the token in a test shows it is an
empty string.

**Cause.** The Bruno CLI runs the `seller` folder **after every other folder**, whatever its `seq`
says. Its `folder.yml` is in the old format (`meta:` with `name` and `seq: 9`), while every other
folder uses `info:`; the CLI orders it last. `sellerToken` and `sellerId` are set inside that folder,
so any request elsewhere runs before they exist. It also means the `seller` folder runs after
`security-checks`.

**Fix.** Put every request that uses the seller's token or id **inside `bruno/seller/`**, with a
`seq` after the request that signs the seller in. Found while adding reviews (specs/046).

### 10.3 Bruno: `search without diacritics` fails after many runs

**Symptom.** `product/search without diacritics` fails its second test - `"may anh" finds "Máy ảnh"` -
while the search itself answers 200 and returns products that all look right.

**Cause.** Debris. Every collection run creates a product called `Máy ảnh Bruno` (and
`verify-saga.sh` and `verify-auth.sh` create products of their own), and nothing removes them. The
request asks for `searchTerm=may anh bruno&pageSize=20` and the listing sorts by name, so once more
than twenty earlier runs' products share that name, the one created by *this* run can fall off the
first page and the test cannot find its id.

**Fix.** Delete the test products. Through the API, an administrator removes each one with
`DELETE /api/products/{id}`, which also tells Inventory to drop its stock rows. On a catalogue that
should hold only the seeded cameras, `server/seed/clean-test-debris.py` does it in bulk - but it
deletes **everything** `cameras.json` does not name, sellers' products and any demo data included, so
do not point it at a catalogue that holds anything else. Run it without `--yes` first; it only says
what it would delete.

### 10.4 After a mutation check, unrelated tests fail - or a mutation seems to break everything

**Symptom.** After checking that a test catches a deliberate defect - copy the file to `.bak`, edit
it, `dotnet test`, then `mv file.bak file` - the next `dotnet test` still behaves as if the defect
were there: tests unrelated to the change fail consistently, or the next mutation appears to break
tests it cannot touch.

**Cause.** `mv` restores the `.bak` file with its **old modification time**, from before the mutated
edit. The restored source now looks older than the assembly built from the mutated source, so
MSBuild's incremental build decides nothing changed and the next run tests the **mutated** binary.

**Fix.** Touch the restored file, or restore with `cp` rather than `mv`:

```bash
mv src/.../Thing.cs.bak src/.../Thing.cs && touch src/.../Thing.cs
```

After the last restore, run the whole suite once more before committing. `dotnet build --no-incremental`
also forces the rebuild.

### 10.5 Client tests time out only in the full run

**Symptom.** `npm test` in `client/` fails one or more tests with a Testing Library timeout from
`findBy...` or `waitFor`, different ones on different runs, while each failing file passes when run on
its own (`npx vitest run src/path/to/file.test.tsx`).

**Cause.** Testing Library waits one second by default. The first render in each test file pays for
initialising i18n and loading the module graph, and Vitest runs the suite's 51 files in parallel, so on
a busy machine that first wait sometimes crossed a second.

**Fix.** [#101](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/101) raised the
wait to three seconds for the whole suite, in `client/src/test/setup.ts`:

```ts
configure({ asyncUtilTimeout: 3000 })
```

If a test needs more than that, look for a real problem (a query that never resolves, a network call
the setup file refuses) before raising it again.

### 10.6 Every order stays `Submitted` after a change to settlement or notifications

**Symptom.** Checkout answers 200, the saga completes (Inventory confirms, Payment records the
payment), but `GET /api/orders/{id}` stays `Submitted` for good. Order's log, or its
`OrderSvc...` error queue in RabbitMQ, shows
`InvalidOperationException: The connection is already in a transaction`. Every unit test passes.

**Cause.** The saga's outcome reaches Order through a consumer, and MassTransit's consumer outbox has
already opened a transaction on the `DbContext` before the consumer runs. When notifications were
added (specs/042), `OrderRepository.TrySettleAsync` began opening a transaction of its own to stage
the notice with the settlement - a second transaction on the same connection, which throws. The unit
tests sent the command directly, outside a consumer, so none of them saw it.

**Fix.** Found and fixed before
[#94](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/94) merged:
`TrySettleAsync` checks `_context.Database.CurrentTransaction` and, when one is open, **joins** it
instead of starting another. `NotificationTests.Settling_inside_a_consumer_transaction_joins_it` opens
the transaction the way MassTransit does and fails without the branch. Any new repository method that
takes a `stage` callback and can be reached from a consumer needs the same branch - see
[reliable messaging §2.3](../architecture/reliable-messaging-and-outbox-pattern.md#23-the-trap-a-consumer-already-holds-a-transaction).
