# Running the System in Containers

**Added**: 2026-09-17 · Spec: [specs/005-containerise-services](../../specs/005-containerise-services/)
· Issues [#6](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/6),
[#7](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/7)

There are two ways to run this system, and both are supported.

| | Command | What runs where |
| :--- | :--- | :--- |
| **Containers** | `docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build` | Everything in containers |
| **Host** (the original) | `docker compose up -d` then `./start-dev.sh` | Infrastructure in containers, services on your machine |

`docker compose up -d` on its own still brings up **infrastructure only** — six PostgreSQL
containers, RabbitMQ and pgAdmin. That is deliberate: the services live in an *overlay* file so that
`start-dev.sh` keeps working untouched. Merging the two would force every contributor down the
container path.

---

## 1. Quick start

**Step-by-step startup, both paths, lives in [Getting Started](../guides/getting-started.md).** This
page is the container reference behind it.

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

Ports are unchanged from the host path: gateway on `5000`, services on `5056`–`5061`. Inside their
containers every service binds `8080`; compose maps it.

```bash
# What is running, and whether it is actually healthy
docker compose -f docker-compose.yml -f docker-compose.app.yml ps

# Logs for one service
docker compose -f docker-compose.yml -f docker-compose.app.yml logs -f catalog

# Back to infrastructure-only, for start-dev.sh
docker compose -f docker-compose.yml -f docker-compose.app.yml down
docker compose up -d
```

**Expect the first build to take several minutes** — it was 7m03s on the machine this was developed
on, against a 5-minute target that was missed. The second build was 24 seconds, because project
files are restored in a layer above the source. If your *second* build is also slow, something has
broken that layering.

---

## 2. Before you trust any result: check the host

```bash
# Windows
Get-Process | Where-Object { $_.ProcessName -like 'Ecommerce.*' }
# Linux / macOS
pgrep -fa Ecommerce.
```

**This must be empty.** A stray `dotnet run` left over from an earlier session holds the host port
and *shadows* the container behind it — your request never reaches the container, and the symptom
is whatever that stale process happens to do.

This is not hypothetical. During the development of this feature a leftover Payment process,
started with `PAYMENT_OUTCOME=Reject`, sat on port 5061 while the Payment container ran behind it.
Orders failed for no visible reason and the containers took the blame for a while.
[Troubleshooting §6](../guides/troubleshooting.md) covers the general shape of this trap.

---

## 3. Configuration

Nothing is baked into an image. Everything that makes one running copy different from another
arrives as an environment variable — the full surface is in
[specs/005-containerise-services/contracts/configuration.md](../../specs/005-containerise-services/contracts/configuration.md).

### Precedence

```text
environment variable  >  .env  >  appsettings.json  >  hardcoded default
```

**This changed on 2026-09-17.** The `.env` loader in every `Program.cs` used to call
`Environment.SetEnvironmentVariable` unconditionally, so the file won. It now fills in only what the
environment has not set. `PAYMENT_OUTCOME=Reject dotnet run ...` used to be silently ignored and now
works.

That inversion is also what made containers possible at all: a settings file that reached an image
would otherwise have overridden everything injected at run time, and the same image would have
behaved identically everywhere no matter what it was told.

### Three settings that are easy to get wrong

| Setting | In containers | Why it bites |
| :--- | :--- | :--- |
| `*_DB_PORT` | **`5432`** | The 5433–5438 in `.env` are *host* publications. Inside the container network every PostgreSQL listens on 5432. Getting this wrong looks exactly like a dead database |
| `JWT_SECRET` | identical for all seven | Identity signs with it, everyone else validates with it. A mismatch is a **401 that looks like a permissions bug** |
| `RABBITMQ_PASS` **and** `RABBITMQ_PASSWORD` | both, same value | The services read the first; `docker-compose.yml` and `.env.example` use the second. A non-default password needs both |

---

## 4. Migrations

In the container path each service applies its own migrations at startup, because
`RUN_MIGRATIONS_ON_STARTUP=true` is set in the overlay.

**It is off everywhere else, on purpose.** A service that migrates on every start needs permission
to alter its own schema forever, and "started successfully" and "was allowed to change the schema"
should not be the same event in a real deployment.

The host path is unchanged: `start-dev.sh` runs `dotnet ef database update` six times before
launching anything. A runtime image has neither the SDK nor the source, so that route does not exist
inside a container — which is why this setting had to exist at all. Before 2026-09-17 **nothing**
called `Database.Migrate()`, so a container stack would have come up healthy and empty.

---

## 5. Images

One [Dockerfile](../../server/Dockerfile) builds all seven services, selected by a `PROJECT` build
argument:

```bash
cd server
docker build --build-arg PROJECT=src/Services/Catalog/Ecommerce.Catalog.WebApi -t ecommerce-catalog .
```

Seven near-identical files would drift — a fix applied to six of them is invisible and nothing
fails.

### `.dockerignore` is not optional

**Docker does not read `.gitignore`.** `server/.env` is correctly kept out of version control and a
`COPY . .` would put it into an image layer anyway, carrying a real `JWT_SECRET`, `DB_PASSWORD` and
`ADMIN_PASSWORD`. `server/.dockerignore` is what prevents that, and it is read from the build
context — `server/`, not the repository root.

### Checking that an image carries no secret

```bash
.github/scripts/verify-image-has-no-secrets.sh ecommerce-catalog:test
```

CI runs this on every push. **It inspects layers, not the running container**, and that distinction
is the whole point:

```bash
docker run --rm <image> ls -la /app     # proves nothing
```

A `COPY . .` followed by `RUN rm .env` leaves the file fully readable in the earlier layer. Layers
are additive; deleting a file does not remove it. The first version of this script scanned the wrong
level of nesting — `docker save` emits an OCI layout with **gzipped** layers, so listing the outer
tar shows digest names and never a file path — and it reported a clean pass on an image that
provably leaked. It now decompresses each blob and reports how many layers it actually read, so a
scan of nothing fails instead of passing.

**If it ever fails, rotate the credentials.** Rebuilding is not enough: an image built once may
already have been pulled, and a distributed secret must be replaced, not deleted.

---

## 6. The gateway is configured differently, and here is why

The gateway is the only service that needs to know where the *others* are. Its `appsettings.json`
points at `http://localhost:50XX`, which is wrong inside a container.

Its YARP destinations are overridden with **command-line arguments**, not environment variables:

```yaml
command:
  - "--ReverseProxy:Clusters:catalog-cluster:Destinations:destination1:Address=http://catalog:8080/"
```

The documented environment-variable form —
`ReverseProxy__Clusters__catalog-cluster__Destinations__destination1__Address` — **does not bind
here.** Verified: the variable is present in the container and YARP still dials `localhost:5057`.
On that same container `Logging__LogLevel__Default=Warning` takes effect, so the environment
provider itself works, and the colon-separated form failed too. Command-line arguments bind and were
verified end to end.

**The cause was not established.** This is written down as an observation rather than an
explanation, so that whoever picks it up starts from what was actually tested. If you work it out,
[research D4](../../specs/005-containerise-services/research.md) is the place to record it.

---

## 7. What this does not do

- **Publish images anywhere.** Nothing is tagged, pushed or rollable-back-to —
  [#8](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/8).
- **Make a schema change survive an image rollback.** Redeploying an older image does not undo a
  migration, and a dropped column will break it —
  [#9](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/9).
- **Run on more than one machine.** No cluster, no scaling, no service mesh.
- **Introduce a secret manager.** It stops secrets being copied where they do not belong; it does
  not manage them.

**Nothing in CI runs the compose stack.** CI builds one image and scans it for secrets; every other
check in this document is something a person has to run. A regression in the container path will not
be caught automatically.
