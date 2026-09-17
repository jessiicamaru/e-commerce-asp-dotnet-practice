# Getting Started — Running the Project

Two supported ways to run this system. Both work; pick by what you are doing.

| | Use when | Command |
| :--- | :--- | :--- |
| **A. Containers** | You want the whole thing running with one command, or you are checking it behaves the way it will elsewhere | `docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build` |
| **B. Host** | You are writing code and want a debugger attached, or fast rebuilds of one service | `docker compose up -d` then `./start-dev.sh` |

Either way the ports are the same: gateway on **5000**, services on **5056–5061**.

---

## 0. Prerequisites

| Path A (containers) | Path B (host) |
| :--- | :--- |
| Docker Engine with Compose v2 | Docker Engine with Compose v2 |
| — | .NET 10 SDK |
| — | `dotnet-ef` (`dotnet tool install --global dotnet-ef`) |

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
docker compose up -d     # infrastructure only: 6 PostgreSQL, RabbitMQ, pgAdmin
./start-dev.sh           # or ./start-dev.ps1 on Windows PowerShell
```

`start-dev` applies the six migrations with `dotnet ef database update` and then launches all seven
services. Nothing about this changed when containers were added — `docker compose up -d` on its own
still brings up infrastructure only, which is exactly what this path expects.

To run one service by hand:

```bash
dotnet run --project src/Services/Catalog/Ecommerce.Catalog.WebApi/
```

---

## 2. Check it is really up

```bash
for p in 5056 5057 5059 5060 5061; do
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

The fastest proof that everything is wired up.

```bash
# 1. Log in as the seeded administrator
TOKEN=$(curl -s -X POST http://localhost:5056/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"<ADMIN_EMAIL>","password":"<ADMIN_PASSWORD>"}' \
  | python -c "import sys,json; print(json.load(sys.stdin)['token'])")

# 2. Create a category, then a product (no stockQuantity - Catalog does not own stock)
#    POST /api/categories   {"name","description","slug","parentCategoryId"}
#    POST /api/products     {"name","description","price","sku","categoryId"}

# 3. Give it stock, through Inventory - this is the service that owns it
curl -X PUT http://localhost:5060/api/stock/<productId> \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"quantityOnHand":10}'

# 4. Register a shopper, submit an order, then read it back
#    POST /api/auth/register  -> token
#    POST /api/orders         {"items":[{productId, productName, quantity, unitPrice}]}
#    GET  /api/orders/{id}    -> "Completed" within a few seconds
```

Two things to notice, because they are recent and deliberate:

- **A newly created product reads `"availability": "OutOfStock"`** until you stock it through
  Inventory. The catalogue reports what the stock owner last told it and never a count of its own.
- **The order settles by itself.** `GET /api/orders/{id}` moves from `Submitted` to `Completed` when
  the saga finishes, or to `Failed` with a reason when it does not.

### Exercising the failure path

The payment service is a stand-in that moves no money. Set `PAYMENT_OUTCOME=Reject` in `server/.env`,
restart Payment, and an order will fail and release its held stock.

```bash
curl -s http://localhost:5061/health   # confirm "configuredOutcome" took effect
```

Set it **in `.env`**, not on the command line — although since 2026-09-17 an exported variable does
win over the file, so `PAYMENT_OUTCOME=Reject dotnet run ...` works too on path B.

---

## 4. Running the tests

```bash
cd server
DB_PASSWORD=<your password> dotnet test
```

59 tests across four projects, all against a **real PostgreSQL** on the ports in `.env` — the
guarantees under test are row locking, unique constraints and guarded updates, which an in-memory
provider does not implement. The database containers must be up.

---

## Where to go next

- [Running in Containers](../infrastructure/running-in-containers.md) — configuration surface, image
  builds, secret scanning
- [Database Setup & Migrations](../infrastructure/database-setup.md) — the `dotnet ef` reference
- [Troubleshooting](./troubleshooting.md) — compile errors, port shadowing, container traps
- [Microservices Design](../architecture/microservices-design.md) — topology, ownership, how the
  services run
- [`CLAUDE.md`](../../CLAUDE.md) — the operational detail, and the traps this codebase has already
  sprung
