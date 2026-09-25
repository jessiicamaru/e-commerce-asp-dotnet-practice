# Running the System in Containers

**Added**: 2026-09-17 · Spec: [specs/005-containerise-services](../../specs/005-containerise-services/)
· Issues [#6](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/6),
[#7](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/7)

There are two ways to run this system, and both are supported.

| | Command | What runs where |
| :--- | :--- | :--- |
| **Containers** | `docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build` | Everything in containers |
| **Host** (the original) | `docker compose up -d` then `./start-dev.sh` | Infrastructure in containers, services on your machine |

`docker compose up -d` on its own still brings up **infrastructure only** — eight PostgreSQL
containers, RabbitMQ, Seq, pgAdmin and Mailpit (every email the stack sends, at http://localhost:8025 - specs/060). That is deliberate: the services live in an *overlay* file so that
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

The overlay, [`docker-compose.app.yml`](../../server/docker-compose.app.yml), runs ten containers - the
eight services, the gateway and the storefront. Open **http://localhost:8088** for the storefront:

| Container | Host port(s) | Inside | Health check | Waits for |
| :--- | :--- | :--- | :--- | :--- |
| `ecommerce-gateway` | `5000` | `8080` | `/health` | Identity, Catalog, Order, Inventory, Payment healthy |
| `ecommerce-identity` | `5056` REST, `6056` gRPC | `8080`, `8081` | `/health` | its database |
| `ecommerce-catalog` | `5057` REST, `6057` gRPC | `8080`, `8081` | `/health` | its database, RabbitMQ |
| `ecommerce-orchestrator` | `5058` | `8080` | **disabled** - no controllers, no `/health` | its database, RabbitMQ |
| `ecommerce-order` | `5059` | `8080` | `/health` | its database, RabbitMQ |
| `ecommerce-inventory` | `5060` | `8080` | `/health` | its database, RabbitMQ, Catalog healthy |
| `ecommerce-payment` | `5061` | `8080` | `/health` | its database, RabbitMQ |
| `ecommerce-cart` | `5062` REST, `6062` gRPC | `8080`, `8081` | `/health` | its database, RabbitMQ |
| `ecommerce-activity` | `5063` | `8080` | `/health` | its database, RabbitMQ |
| `ecommerce-storefront` | `8088` | `8080` | `/` answers | the gateway healthy |

Ports are unchanged from the host path for REST. Inside their containers every service binds `8080`,
and the three that serve gRPC also bind `8081`; callers inside the network use `http://catalog:8081`,
`http://cart:8081` and `http://identity:8081` (`CATALOG_GRPC_ADDRESS`, `CART_GRPC_ADDRESS`,
`IDENTITY_GRPC_ADDRESS`). Under `start-dev` the gRPC ports are `5156`, `5157` and `5162` instead.

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
| `*_DB_PORT` | **`5432`** | The 5433–5440 in `.env` are *host* publications. Inside the container network every PostgreSQL listens on 5432. Getting this wrong looks exactly like a dead database |
| `JWT_SECRET` | identical for every service | Identity signs with it, everyone else validates with it. A mismatch is a **401 that looks like a permissions bug** |
| `RABBITMQ_PASS` **and** `RABBITMQ_PASSWORD` | both, same value | The services read the first; `docker-compose.yml` and `.env.example` use the second. A non-default password needs both |

---

## 4. Migrations

In the container path each service applies its own migrations at startup, because
`RUN_MIGRATIONS_ON_STARTUP=true` is set in the overlay.

**It is off everywhere else, on purpose.** A service that migrates on every start needs permission
to alter its own schema forever, and "started successfully" and "was allowed to change the schema"
should not be the same event in a real deployment.

The host path is unchanged: `start-dev.sh` runs `dotnet ef database update` once per database before
launching anything. A runtime image has neither the SDK nor the source, so that route does not exist
inside a container — which is why this setting had to exist at all. Before 2026-09-17 **nothing**
called `Database.Migrate()`, so a container stack would have come up healthy and empty.

**Order matters in Identity**, the one service that seeds at startup: the migration must run before
the seeding reads the `roles` table. It did not until
[#100](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/100), so a fresh Identity
container on an empty volume crash-looped with `relation "roles" does not exist` - see
[troubleshooting §10.1](../guides/troubleshooting.md#101-a-fresh-identity-container-crash-loops-with-relation-roles-does-not-exist).

---

## 4.5 Volumes, and starting from empty

| Volume | Defined in | Holds |
| :--- | :--- | :--- |
| `postgres_identity_data`, `postgres_catalog_data`, `postgres_order_data`, `postgres_orchestrator_data`, `postgres_inventory_data`, `postgres_payment_data`, `postgres_cart_data`, `postgres_activity_data` | `docker-compose.yml` | one PostgreSQL data directory each |
| `rabbitmq_data` | `docker-compose.yml` | the broker's queues and messages |
| `seq_data` | `docker-compose.yml` | logs and traces |
| `catalog_images` | `docker-compose.app.yml` | product and variant photographs (specs/019, 032) |

`catalog_images` is mounted on `/app/data`, **not** on the `product-images` subdirectory
(`ProductImages__Root` is `/app/data/product-images`): a mount point the image lacks is created
root-owned, and the non-root service then fails its startup write check. The store creates the
subdirectory itself. A directory on one volume assumes **one** Catalog instance - two instances would
each see only their own images, and the orphan reclaim (specs/033) would report the other's images as
orphans.

Compose prefixes volume names with the project name - `server_` when run from `server/` - so
`docker volume ls` shows `server_postgres_catalog_data`, `server_catalog_images` and so on.

**To start from empty**, stop the stack and remove the volumes; the migrations recreate every schema
at the next start:

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml down -v   # -v removes the named volumes
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

`down -v` also removes `seq_data`; Seq then asks for its first-run password again. To keep the logs,
remove the other volumes by name with `docker volume rm` instead. Reseeding is in
[Getting Started](../guides/getting-started.md#seeding-a-demo-dataset).

---

## 5. Images

One [Dockerfile](../../server/Dockerfile) builds every service, selected by a `PROJECT` build
argument:

```bash
cd server
docker build --build-arg PROJECT=src/Services/Catalog/Ecommerce.Catalog.WebApi -t ecommerce-catalog .
```

One file per service would drift — a fix applied to all but one is invisible and nothing fails. The
Dockerfile copies each `.csproj` by name before restoring, so **a new project needs a line there**,
or the build fails at publish with a message about the project rather than about the list.

### The storefront image

The storefront has its own [Dockerfile](../../client/Dockerfile) (specs/051): Node builds the bundle,
and an unprivileged nginx (uid 101, port 8080) serves it and forwards `/api` to the gateway - one origin,
exactly as Vite's dev proxy gives in development, so there is no CORS and Identity's HttpOnly refresh
cookie keeps working.

```bash
docker build -t ecommerce-storefront client
docker run -p 8088:8080 -e GATEWAY_URL=http://gateway:8080 ecommerce-storefront
.github/scripts/verify-storefront-image.sh ecommerce-storefront   # does it actually serve the storefront?
```

| What | Why |
| :-- | :-- |
| `GATEWAY_URL` is read when the container **starts** | nginx's image renders `/etc/nginx/templates/*.template` with `envsubst` at start, for defined variables only, so one image runs against any gateway |
| Any path that is not a file serves `index.html` | a reload of `/orders/123` is a route the browser-side router knows and nginx does not |
| `/assets/*` that does not exist is **404**, not `index.html` | a browser would run the HTML as JavaScript; a stale bundle must fail loudly |
| `index.html`: `Cache-Control: no-cache`; `/assets/*`: a year, `immutable` | assets are named by content hash, so a new release is picked up at the next load |
| `client_max_body_size 3m` | Catalog accepts a 2 MB photograph (`ProductImageKey.MaxBytes`); nginx's default 1 MB would answer 413 before Catalog saw it. Keep it above `MaxBytes` |
| Build context `client/`, and the image runs `vite build` only | `npm run build` also type-checks the tests, one of which imports a file from `server/` (specs/048). Tests and type-checking are CI's `client` job, which `publish` waits for |

⚠️ **Open it on `localhost`.** Identity's refresh cookie is `Secure`, and browsers accept a `Secure`
cookie over plain HTTP only from `localhost`. From another machine by IP, signing in works until the first
refresh and then the session is lost - that needs TLS in front, which is deployment and out of scope.

`verify-storefront-image.sh` starts the image against a stand-in gateway and asks what a browser would:
the app, a deep link, a missing asset, `/api` forwarded with its path and query, a 2 MB upload, and the
cache headers. CI runs it on every change, in the `client` job.

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

### The `edge` network: which proxy the gateway believes (specs/062)

The storefront's nginx forwards every browser's requests to the gateway, so the gateway would see one
address, nginx's, for everybody. The limits on sign-in would then be shared by every visitor. So:

- the storefront sits on a small network of its own, `edge` (`172.30.10.0/24`), at a **fixed** address,
  `172.30.10.10`;
- the gateway joins both `edge` and the default network, and `GATEWAY_TRUSTED_PROXIES=172.30.10.10`
  makes it believe that address's `X-Forwarded-For`, and nobody else's;
- nginx appends `$remote_addr` to `X-Forwarded-For`, and the gateway takes only that last hop, so whatever
  a browser wrote into the header itself is ignored.

Traffic from the host straight to `:5000` is counted by the address Docker gives it, and its
`X-Forwarded-For` is ignored.

---

## 7. Published images

Since 2026-09-19, a merge to `main` whose checks pass publishes these same images to GHCR - nine of
them: `identity`, `catalog`, `order`, `orchestrator`, `inventory`, `payment`, `cart`, `activity` and
`gateway`:

```bash
docker pull ghcr.io/jessiicamaru/ecommerce-catalog:sha-<short-sha>
```

Each is built, **scanned for credentials, and only then pushed** — in that order, because once an
image is published the scanned bytes and the shipped bytes have to be the same artifact. Every image
is built and scanned before any is pushed; a partial release is not a release.

`:main` also exists and **moves**, so it can never name "the version from before". Only `sha-` tags
may name something deployable.
[release-artifacts.md](../../specs/006-release-and-rollback/contracts/release-artifacts.md) has the
guarantees; a published image takes the same configuration as one you build locally.

**A schema change can still strand an older image.** Dropping, renaming or narrowing a column means
redeploying a previous version takes the service down rather than restoring it. The constitution now
carries the expand/contract rule, and a `schema-compatibility` job says so on any pull request that
adds such a migration — without blocking it.

## 8. What this does not do
- **Run on more than one machine.** No cluster, no scaling, no service mesh.
- **Introduce a secret manager.** It stops secrets being copied where they do not belong; it does
  not manage them.

**Nothing in CI runs the compose stack.** CI builds one image and scans it for secrets, and its smoke
jobs (`auth-smoke`, `saga-e2e`) start the services with `dotnet run` against service containers of
their own; every other check in this document is something a person has to run. A regression in the
container path will not be caught automatically - which is how the Identity startup-order defect
(#100) went unnoticed until a stack was wiped.
