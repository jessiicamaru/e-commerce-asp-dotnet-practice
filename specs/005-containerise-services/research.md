# Phase 0 Research: Run the System in Containers

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-17

Seven decisions, each with what was chosen, why, and what was rejected.

---

## D1 — One Dockerfile, parameterised

**Decision**: A single `server/Dockerfile`, multi-stage, taking a `PROJECT` build argument naming the
WebApi project to publish. Compose passes it per service.

**Rationale**: The seven WebApi projects are structurally identical — same target framework, same
shape, differing only in path. Seven files that must stay in sync are seven files that will not: a fix
applied to six of them is invisible, and nothing fails.

The build order matters as much as the file count. Copying every `.csproj`, restoring, and *then*
copying the source means a code change does not invalidate the restore layer. Without it, every build
re-downloads every package, and SC-004's five minutes is spent on the first service.

**Alternatives considered**:

- **Seven Dockerfiles, one per service.** More conventional, easier to read one at a time, and the
  usual answer. Rejected on the drift argument: this repository has already had a defect where a
  pattern applied to one service and not another went unnoticed (the `OrderCompletedConsumer` queue
  collision in feature 003), and that was two files, not seven.
- **A single "all services" image** with an entrypoint selecting which to run. Smaller registry
  footprint, and wrong: one image that can be seven things cannot be rolled back independently, which
  is the point of the exercise (#8).

---

## D2 — Listening address from configuration, with today's value as the default

**Decision**: Replace `app.Run("http://localhost:50XX")` with a conditional:

```csharp
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrEmpty(urls))
{
    app.Run("http://localhost:5057");   // unchanged local default
}
else
{
    app.Run();                          // honour ASPNETCORE_URLS
}
```

In containers every service binds `http://+:8080` and compose maps it to today's host port.

**Rationale**: FR-002 and FR-012 together. Binding `localhost` inside a container accepts connections
from that container only, however the ports are published — so the pinned address has to go. But
dropping the argument entirely would make every service default to ASP.NET's own port and collide
under `start-dev.sh`, which FR-012 forbids.

Standardising on 8080 *inside* while keeping 5056–5061 *outside* is what keeps the gateway config, the
port table in `CLAUDE.md` and every quickstart in this repository true.

**Alternatives considered**:

- **`ASPNETCORE_URLS` unconditionally, defaulted in `launchSettings.json`.** Cleaner code, but
  `launchSettings.json` is not read by `dotnet run --no-launch-profile`, which is exactly how CI and
  `start-dev.sh` invoke these services. It would work locally and break in CI — the worst failure
  shape.
- **Keeping distinct ports inside containers too.** No benefit; container-internal ports are addressed
  by service name and never collide.

---

## D3 — Migrations: nothing currently creates the schema

**Decision**: Each service applies its own migrations at startup, **only when explicitly told to**:

```csharp
if (Environment.GetEnvironmentVariable("RUN_MIGRATIONS_ON_STARTUP") == "true")
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<TDbContext>().Database.MigrateAsync();
}
```

The compose overlay sets it to `true`. Nothing else does.

**Rationale**: This is the thing the spec did not anticipate, and it was found by looking rather than
assuming:

```bash
$ grep -rn "Database.Migrate()" server/src --include=*.cs | grep -v obj
# no matches
```

Schemas are created today by `start-dev.sh` shelling out to `dotnet ef database update` six times, and
by CI doing the same. Neither is available inside a container — `dotnet ef` needs the SDK and the
source, and a runtime image has neither. Without this, FR-011's "one command from a fresh checkout"
produces seven healthy services and zero tables.

Gating it on a variable keeps the production path clean. A service that migrates on every start needs
permission to alter its own schema forever, which is a privilege worth not granting by default, and it
interacts badly with rolling back to an older image
([#9](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/9)). Default off, on in
compose, is the combination that makes the dev path one command without deciding the deployment
question this feature has no business deciding.

**Alternatives considered**:

- **Migrate unconditionally at startup.** Simplest, and common in tutorials. Rejected: it makes
  "started successfully" and "was allowed to change the schema" the same event, forever.
- **A one-shot migration container per service**, with `depends_on: service_completed_successfully`.
  Conceptually the cleanest — deployment stays out of the application — and rejected as
  disproportionate: six more compose services and an SDK-sized image, to solve a problem one
  environment variable solves for a single-instance development stack. Worth revisiting when there is
  a real deployment.
- **Leaving `dotnet ef` on the host.** Rejected: it contradicts SC-005's "0 processes on the host" and
  requires the SDK installed, which a fresh checkout on a clean machine does not have.

---

## D4 — The gateway's destinations, without touching code

**Decision**: Override the YARP cluster addresses with environment variables in compose, using
ASP.NET Core's built-in configuration binding:

```text
ReverseProxy__Clusters__catalog-cluster__Destinations__destination1__Address=http://catalog:8080/
```

No change to `Program.cs` or `appsettings.json`.

**Rationale**: The gateway is the one service whose configuration genuinely differs between host and
container — it is the only one that needs to know where the *others* are:

```bash
$ # Clusters in appsettings.json
identity-cluster       http://localhost:5056/
catalog-cluster        http://localhost:5057/
order-cluster          http://localhost:5059/
inventory-cluster      http://localhost:5060/
payment-cluster        http://localhost:5061/
```

The double-underscore syntax already reaches arbitrary configuration depth, so this needs no code at
all. It is verbose in the compose file, and that verbosity is honest: it shows exactly which value is
being replaced.

**Alternatives considered**:

- **An `appsettings.Container.json` selected by `ASPNETCORE_ENVIRONMENT`.** Much more readable. Rejected
  because it puts the container's topology inside the image, so the image differs by environment in
  fact if not in bytes — and two config files describing the same clusters will diverge.
- **Code that rewrites destinations from a variable.** Rejected: code to do what the framework already
  does.

---

## D5 — Starting in the wrong order, which is normal

**Decision**: Two layers, because they fail differently.

1. **Compose `depends_on` with `condition: service_healthy`** for PostgreSQL and RabbitMQ, using their
   existing health checks. This handles the common case.
2. **`EnableRetryOnFailure()` on the Npgsql connection**, because `depends_on` only waits for the
   container, and a database that is accepting connections is not always ready to serve them.

MassTransit already retries its broker connection on its own; nothing is needed for RabbitMQ beyond
`depends_on`.

**Rationale**: FR-009. Containers start in parallel, so a service reaching its database before the
database is listening is not a failure — it was early. This project has already lost a CI run to
exactly this shape: Catalog never became healthy because no broker was running, which looked like a
Catalog defect.

**Alternatives considered**:

- **`depends_on` alone.** Rejected — it waits for the health check, not for readiness under load, and
  the gap is where flaky startups live.
- **A `wait-for-it` script in the entrypoint.** Rejected: it moves a retry policy into shell, where it
  cannot be tested and cannot distinguish "not yet" from "wrong password".
- **`restart: unless-stopped` and letting it crash-loop into working.** It does eventually work, and it
  makes every genuine startup failure look identical to a timing problem.

---

## D6 — `.dockerignore` before the first Dockerfile

**Decision**: `.dockerignore` is written and committed **first**, as its own step, before any Dockerfile
exists. At minimum: `**/.env*`, `**/bin/`, `**/obj/`, `.git/`, `**/TestResults/`, `specs/`, `docs/`.

**Rationale**: Docker does not read `.gitignore`. `server/.env` is correctly gitignored and has never
been committed — and a `COPY . .` would put it in a layer anyway, carrying a real `JWT_SECRET`,
`DB_PASSWORD` and `ADMIN_PASSWORD`.

The ordering is the decision, not the file. Written afterwards, the guard arrives after the moment it
was needed: an image built once is an image that may have been shared, and a distributed credential
must be rotated rather than deleted. Written first, the mistake is impossible.

`JWT_SECRET` is the one that matters most. It is the signing key every service validates against, so
anyone who can read it can mint a token for any user and any role — principle IV's guarantee rests
entirely on it staying secret.

**Alternatives considered**:

- **Careful `COPY` statements naming only what is needed**, with no `.dockerignore`. It works, and it
  depends on every future edit staying careful. Rejected: a guard that relies on discipline is not a
  guard. Both, in fact — the Dockerfile copies narrowly *and* the ignore file exists.
- **Relying on `.gitignore`.** The thing that does not work, and the reason this issue exists.

---

## D7 — How "no secret in the image" is verified

**Decision**: A script in CI and in the quickstart that inspects **layer history**, not the final
filesystem:

```bash
docker history --no-trunc <image>          # every layer's command
docker save <image> | tar -t | grep -i env # every file in every layer
```

and asserts the known variable names and values appear nowhere.

**Rationale**: Constitution V, applied to the specific way this check is usually got wrong. Running
`docker run --rm <image> ls /app` proves only what survived to the final layer. A `COPY . .` followed
by `RUN rm .env` leaves the file fully readable in the earlier layer, and the naive check passes.

FR-006's wording — *"at any point in its contents, not merely in its final state"* — exists because of
this, and the verification has to match the wording or the requirement is decorative.

**Alternatives considered**:

- **Inspecting the running container's filesystem.** The naive check above. Rejected as described.
- **Trusting `.dockerignore`.** It is the mechanism; this is the check that it worked. Trusting the
  mechanism you just wrote is how the mechanism turns out to have a typo.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| Fixing `.env` precedence surprises someone relying on the current behaviour | A value they exported now wins where the file used to | Accepted, and it is the correct precedence. Goes in `CLAUDE.md`'s Gotchas, where the *current* behaviour is already recorded as a trap |
| `RUN_MIGRATIONS_ON_STARTUP` is left on somewhere it should not be | A service is allowed to alter its schema at every start | Default off, on only in the compose overlay, and named so that reading it is enough to understand it |
| The compose overlay and `start-dev.sh` drift | Someone fixes one and not the other; the container path and the script path diverge silently | Not solved. Both are exercised by quickstart scenarios, which is a check somebody has to run rather than one that runs itself |
| Image build is slow enough that people stop using it | SC-004's five minutes is missed and the container path is abandoned | Layer ordering in D1 is the mitigation; the second build is the one that matters and it should be seconds |
| A token minted by Identity is rejected elsewhere because `JWT_SECRET` differs per container | Looks exactly like an authorization bug | One compose-level source for the variable, and a quickstart scenario that asserts cross-service token acceptance specifically |
