# Getting Started — Running the Project

Two supported ways to run this system. Both work; pick by what you are doing.

| | Use when | Command |
| :--- | :--- | :--- |
| **A. Containers** | You want the whole thing running with one command, or you are checking it behaves the way it will elsewhere | `docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build` |
| **B. Host** | You are writing code and want a debugger attached, or fast rebuilds of one service | `docker compose up -d` then `./start-dev.sh` |

Either way the REST ports are the same: gateway on **5000**, services on **5056–5063** (Identity
5056, Catalog 5057, Orchestrator 5058, Order 5059, Inventory 5060, Payment 5061, Cart 5062, Activity
5063). The gRPC ports used between services differ: **6056** (Identity), **6057** (Catalog) and
**6062** (Cart) on the host in the container path, **5156**, **5157** and **5162** under `start-dev`.
The storefront, when you run it, is on **5173**.

There is a third way that skips building entirely — pull a published image:

```bash
docker pull ghcr.io/jessiicamaru/ecommerce-catalog:sha-<short-sha>
```

Every merge to `main` publishes one of these per service, each named by the commit it came from and never
overwritten. Use it to run an exact past version without checking that commit out. The `:main` tag
also exists for convenience, but it moves, so it can never name "the version from before" —
[release-artifacts.md](../../specs/006-release-and-rollback/contracts/release-artifacts.md) has the
guarantees.

---

## 0. Prerequisites

| Path A (containers) | Path B (host) |
| :--- | :--- |
| Docker Engine with Compose v2 | Docker Engine with Compose v2 |
| — | .NET 10 SDK |
| — | `dotnet-ef` (`dotnet tool install --global dotnet-ef`) |
| Node.js and npm, only for the storefront | Node.js and npm, only for the storefront |
| Python 3, only for the seed scripts | Python 3, only for the seed scripts |

## 1. Configure, once

```bash
cd server
cp .env.example .env
```

Then fill it in. Three entries are worth care:

- **`JWT_SECRET`** — generate one, at least 32 bytes: `openssl rand -base64 48`. Every service must
  see the same value; Identity signs tokens with it and the others validate with it.
- **`DB_PASSWORD`** — used by the database containers *and* by the services connecting to them.
- **`ADMIN_EMAIL` / `ADMIN_PASSWORD`** — the first administrator, seeded by Identity at startup while
  no admin exists yet. Self-registration only ever grants `Customer`.

`server/.env` is gitignored, and `server/.dockerignore` keeps it out of images. Neither protects the
other — Docker does not read `.gitignore`.

---

## Path A — Everything in containers

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

The first build takes several minutes (it was 7m03s on the machine this was developed on). Later
builds are seconds, because project files are restored in a layer above the source.

```bash
# Is it actually healthy, not merely running?
docker compose -f docker-compose.yml -f docker-compose.app.yml ps

# Logs for one service
docker compose -f docker-compose.yml -f docker-compose.app.yml logs -f catalog

# Stop
docker compose -f docker-compose.yml -f docker-compose.app.yml down
```

Migrations run themselves here: the overlay sets `RUN_MIGRATIONS_ON_STARTUP=true`, because a runtime
image has neither the SDK nor the source and `dotnet ef` cannot run inside one. That setting is off
everywhere else.

Deeper detail — the configuration surface, image builds, secret scanning — is in
[Running in Containers](../infrastructure/running-in-containers.md).

---

## Path B — Services on your machine

```bash
cd server
docker compose up -d     # infrastructure only: 8 PostgreSQL, RabbitMQ, Seq, pgAdmin
./start-dev.sh           # or ./start-dev.ps1 on Windows PowerShell
```

`start-dev` applies all eight services' migrations with `dotnet ef database update` and then launches
the eight services and the gateway. Nothing about this changed when containers were added — `docker compose up -d` on its own
still brings up infrastructure only, which is exactly what this path expects.

To run one service by hand:

```bash
dotnet run --project src/Services/Catalog/Ecommerce.Catalog.WebApi/
```

---

## 2. Check it is really up

```bash
for p in 5056 5057 5059 5060 5061 5062 5063; do
  printf "%s -> " $p
  curl -s -o /dev/null -w '%{http_code}\n' http://localhost:$p/health
done
curl -s -o /dev/null -w 'gateway -> %{http_code}\n' http://localhost:5000/api/catalog/health
```

The orchestrator (5058) has **no** `/health` — it has no controllers at all. That is expected, not a
failure.

### If something is wrong, check this first

```bash
# Windows
Get-Process | Where-Object { $_.ProcessName -like 'Ecommerce.*' }
# Linux / macOS
pgrep -fa Ecommerce.
```

On path A this must be **empty**. A `dotnet run` left over from an earlier session holds the host
port and *shadows* the container behind it — your request never reaches the container, and the
symptom is whatever the stale process happens to do. The same applies to a natively installed
PostgreSQL on 5432 or RabbitMQ on 5672.

[Troubleshooting §6 and §7](./troubleshooting.md) cover this and the other common traps.

---

## 3. Place an order end to end

The fastest proof that everything is wired up. Everything goes through the gateway on `5000`, as
the storefront does.

```bash
# 1. Log in as the seeded administrator
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"<ADMIN_EMAIL>","password":"<ADMIN_PASSWORD>"}' \
  | python -c "import sys,json; print(json.load(sys.stdin)['token'])")

# 2. Create a category, then a product (no stockQuantity - Catalog does not own stock)
#    POST /api/categories   {"name","description","slug","parentCategoryId"}
#    POST /api/products     {"name","description","price","sku","categoryId"}
#    "price" is in the default currency (VND). A product an administrator lists belongs to the shop
#    and is on sale at once; a seller's product waits for a moderator (specs/045).
#    The product's first variant has the product's own id.

# 3. Give it stock, through Inventory - this is the service that owns it. The id is a VARIANT id.
curl -X PUT http://localhost:5000/api/stock/<variantId> \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"quantityOnHand":10}'

# 4. Register a shopper, fill their cart, check out, then read the order back
#    POST   /api/auth/register    -> token (use it as the shopper's Bearer token below)
#    POST   /api/addresses        {"recipientName","line1","city","postalCode","country"}
#    POST   /api/cart/items       {"productId", "quantity", "variantId"}  (variantId optional)
#    GET    /api/orders/quote     ?addressId=...&shippingOption=express - what it would cost
#    POST   /api/orders           {"addressId", "shippingOption": "express"} - the cart says
#                                 what, the token says who, Catalog and the option say how much
#    GET    /api/orders/{id}      -> "Paid" within a few seconds; an admin then moves the shop's
#                                 parcel to Preparing and Shipped (POST .../preparing, .../shipment)
#    POST   /api/orders/{id}/shipments/{shipmentId}/received   -> the customer confirms delivery
#    GET    /api/cart             -> the ordered lines are gone
```

Every endpoint is in [reference/api.md](../reference/api.md).

The same flow, with every request written out and tested, is the [Bruno collection](../../bruno/)
— open it, pick the `local` environment, fill the two admin variables, and run it.

Things to notice, because they are deliberate:

- **A newly created product reads `"availability": "OutOfStock"`** until you stock it through
  Inventory. The catalogue reports what the stock owner last told it and never a count of its own.
- **The order settles by itself.** `GET /api/orders/{id}` moves from `Submitted` to `Paid` when
  the saga finishes, or to `Failed` with a reason when it does not.
- **The total has parts.** An order shows `subtotal`, `shippingPrice`, `taxTotal`, `discountTotal`
  and `totalAmount`, plus the `taxRate` for its destination — prices exclude tax
  ([ADR-002](../architecture/adr-002-tax-exclusive-prices.md)).
- **Nothing the client sends decides the price.** Checkout charges Catalog's current price for what
  is in the cart; an empty cart is refused with `409`, and Catalog, Cart or Identity being unreachable
  with `503`.
- **Currency and language are per request.** `?currency=USD` (or `X-Currency`) and `?lang=en` (or
  `Accept-Language`). A variant with no price in the requested currency is shown with no price and
  cannot be bought - nothing is converted.
- **The cart empties only when the order completes.** A declined payment leaves it exactly as it was.

### Exercising the failure path

The payment service is a stand-in that moves no money. Set `PAYMENT_OUTCOME=Reject` in `server/.env`,
restart Payment, and an order will fail, release its held stock, and leave the cart alone.

```bash
curl -s http://localhost:5061/health   # confirm "configuredOutcome" took effect
```

Set it **in `.env`**, not on the command line — although since 2026-09-17 an exported variable does
win over the file, so `PAYMENT_OUTCOME=Reject dotnet run ...` works too on path B.

---

## Seeding a demo dataset

A fresh stack has an administrator and nothing else. Two scripts in [`server/seed/`](../../server/seed/)
fill it **through the gateway, as an administrator**, so every row goes down the path a person uses
(validation, the outbox, the stock announcements). Both need a running stack and the administrator's
credentials; neither writes SQL.

```bash
cd server

# Categories and real cameras: variants, VND and USD prices, Vietnamese and English text, stock.
# Idempotent by SKU; it never deletes.
ADMIN_EMAIL=... ADMIN_PASSWORD=... python seed/seed-catalogue.py

# Photographs, from seed/images/ - one file per product, named after its SKU (SONY-A7M4.jpg).
# Without --yes it only says what it would do.
ADMIN_EMAIL=... ADMIN_PASSWORD=... python seed/seed-images.py --yes
```

`seed/images/` is **gitignored on purpose**: this repository is public and a photograph belongs to
whoever took it, so you stage the files yourself; where each one came from is recorded in
[`IMAGE-CREDITS.md`](../../server/seed/IMAGE-CREDITS.md). ⚠️ The prices in `seed/cameras.json` are
approximate - right order of magnitude and right relative order, not quotes from a shop.

A fuller demo dataset - sellers and their shops, customers, orders taken through to delivery, reviews,
moderation decisions, payouts and product views - is loaded by a script kept **outside the
repository**, for local use only. It also goes through the API.

`GATEWAY_URL` points either script at a gateway other than `http://localhost:5000`.

### Cleaning up after the test scripts

`verify-saga.sh`, `verify-auth.sh` and the Bruno collection each create real products on every run and
remove none. `seed/clean-test-debris.py` deletes every product whose SKU is **not** in `cameras.json`
(dry run without `--yes`):

```bash
ADMIN_EMAIL=... ADMIN_PASSWORD=... python seed/clean-test-debris.py --yes
```

⚠️ Keep-list, not delete-list: it also deletes products that a seller or a demo dataset added. Use it
only on a catalogue that should hold the seeded cameras and nothing else.

### Starting again from empty

Migrations run at startup in the container path, so a reset is: stop the stack, remove the database,
RabbitMQ and image volumes, start it again, and reseed.

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml down
docker volume rm server_postgres_identity_data server_postgres_catalog_data server_postgres_order_data \
  server_postgres_orchestrator_data server_postgres_inventory_data server_postgres_payment_data \
  server_postgres_cart_data server_postgres_activity_data server_rabbitmq_data server_catalog_images
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
# wait until `ps` shows the services healthy, then run the seed scripts above
```

(`down -v` does the same in one step but also removes `seq_data`, and Seq then asks for its first-run
password again.) Identity recreates the roles and the administrator from `ADMIN_EMAIL` /
`ADMIN_PASSWORD` on its first start. On the host path, `start-dev` recreates the schemas with
`dotnet ef database update` instead.

---

## The storefront

The React storefront in [`client/`](../../client/) talks only to the gateway, through Vite's
development proxy (`/api` -> `http://localhost:5000`), so the stack must be up first.

```bash
cd client
npm install
npm run dev          # http://localhost:5173
npm test             # unit tests (Vitest), no network
```

`GATEWAY_URL=http://host:port npm run dev` points the proxy elsewhere. Conventions and the rest of the
commands are in [client/README.md](../../client/README.md).

**In containers** the storefront is already running: the overlay builds it with everything else, and it
is at **http://localhost:8088** (specs/051). Open it on `localhost`, not by IP - the refresh cookie is
`Secure` - see [running in containers](../infrastructure/running-in-containers.md#the-storefront-image).

---

## 4. Running the tests

```bash
cd server
DB_PASSWORD=<your password> dotnet test
```

Seven test projects — Catalog, Order, Inventory, Payment, Cart, Identity and Activity — all against a
**real PostgreSQL** on the ports in `.env`: the guarantees under test are row locking, unique
constraints and guarded updates, which an in-memory provider does not implement. The database
containers must be up. The storefront's tests are `npm test` in `client/`.

End-to-end checks that need running services: `.github/scripts/verify-auth.sh` (Identity and Catalog,
plus Order if it is up) and `.github/scripts/verify-saga.sh` (all six checkout services), both run
from `server/` with `ADMIN_EMAIL` / `ADMIN_PASSWORD` set.

---

## Where to go next

- [Running in Containers](../infrastructure/running-in-containers.md) — configuration surface, image
  builds, secret scanning
- [Database Setup & Migrations](../infrastructure/database-setup.md) — the `dotnet ef` reference
- [Troubleshooting](./troubleshooting.md) — compile errors, port shadowing, container traps
- [Bruno collection](../../bruno/) — every public endpoint, runnable and tested
- [Reference](../reference/) — every endpoint, table, message, gRPC method and gateway route,
  generated from the code
- [Microservices Design](../architecture/microservices-design.md) — topology, ownership, how the
  services run
- [`CLAUDE.md`](../../CLAUDE.md) — the operational detail, and the traps this codebase has already
  sprung
