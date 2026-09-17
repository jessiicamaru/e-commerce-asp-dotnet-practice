# Quickstart & Validation: Run the System in Containers

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-17

Nine scenarios. Each names the requirement it proves and what a failure looks like, so that "it
passed" means something specific.

---

## Prerequisites

A checkout, a container runtime, and `server/.env` (copied from `.env.example`). **Nothing else** —
proving that is scenario 4.

> **Check nothing native is shadowing the containers first.** A local PostgreSQL on 5432 or a local
> RabbitMQ on 5672 wins over the container silently, and this repository has lost time to both.
>
> ```bash
> docker ps --format '{{.Names}}\t{{.Ports}}'
> ```

---

## Scenario 1 — The build excludes what it must *(US2, FR-005, SC-003)*

**Run this before building anything**, because it is the check that the guard exists at all:

```bash
cd server
docker build --build-arg PROJECT=src/Services/Catalog/Ecommerce.Catalog.WebApi -t ecommerce-catalog:test .

# The check that matters — layer history, not the final filesystem
docker save ecommerce-catalog:test | tar -t | grep -iE '\.env' && echo "FAIL" || echo "no .env in any layer"
docker history --no-trunc ecommerce-catalog:test | grep -iE 'JWT_SECRET|ADMIN_PASSWORD' && echo "FAIL" || echo "no secret in any build step"
```

**Expect**: both report clean.

**Failure looks like**: a `.env` entry in the tar listing. Note it can be there even when
`docker run --rm ecommerce-catalog:test ls -la /app` shows nothing — a `COPY . .` followed by
`RUN rm .env` leaves the file fully readable in the earlier layer. **That is why this scenario
inspects layers and not the running container**, and checking the running container instead is the
specific mistake this scenario exists to prevent.

**If it fails, the credentials are already compromised in that image.** Rotate `JWT_SECRET`,
`DB_PASSWORD` and `ADMIN_PASSWORD` rather than rebuilding — deleting an image does not recall it.

---

## Scenario 2 — One service, told where its database is *(US1, FR-001, SC-001)*

```bash
docker network create ecom-test
docker run -d --name pg-test --network ecom-test \
  -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=test -e POSTGRES_DB=ecommerce_catalog_db postgres:16-alpine

docker run -d --name catalog-test --network ecom-test -p 18080:8080 \
  -e DB_HOST=pg-test -e DB_PASSWORD=test -e ASPNETCORE_URLS=http://+:8080 \
  -e RUN_MIGRATIONS_ON_STARTUP=true -e JWT_SECRET=test-secret-at-least-32-bytes-long \
  ecommerce-catalog:test

sleep 15 && curl -s http://localhost:18080/health
```

**Expect**: `"status": "Healthy"`, with `catalog_postgres_db` healthy inside it. **0 files edited
inside the image** to achieve it.

**Failure looks like**: a connection error naming `localhost`. That means `DB_HOST` is not being read
and the connection string is still hardcoded — the core of #6.

---

## Scenario 3 — The same image, a different database *(US1 scenario 2, FR-007, SC-002)*

Start a second Postgres, run the **same image** against it, create a category in one, and confirm it
does not appear in the other.

**Expect**: the data is in the database named and not the other.

**Why this and not just scenario 2**: scenario 2 passes for an image that hardcodes any single
database. This is the one that proves the image is not tied to a deployment.

---

## Scenario 4 — One command, from a fresh checkout *(US3, FR-011, SC-004)*

On a machine with only a checkout, a container runtime and `server/.env`:

```bash
cd server
time docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

**Expect**: all containers healthy, **under 5 minutes including the first build**. Then:

```bash
docker compose -f docker-compose.yml -f docker-compose.app.yml ps
```

**Expect**: every service `healthy`, none restarting.

**Failure looks like**: a service in a restart loop. Read its logs before assuming a defect — a
service that started before its database is early, not broken, and scenario 6 is where that is tested
deliberately.

**Then run it again** and record the second time. The first build is allowed to be slow; if the
second is too, the Dockerfile's layer ordering is wrong and people will stop using this path.

---

## Scenario 5 — Nothing on the host *(US3 scenario 2, SC-005)*

With the system up, place an order end to end through the gateway: log in, create a category and a
product, wait for Inventory to register it, set stock, order it.

**Expect**: the order reads `Completed`, stock is deducted, and the catalogue's availability flips to
`OutOfStock` — the feature 004 behaviour, working across container boundaries.

Then the assertion that makes this scenario worth having:

```bash
# Windows
powershell -Command "Get-Process | Where-Object { \$_.ProcessName -like 'Ecommerce.*' }"
# Linux
pgrep -fa Ecommerce.
```

**Expect**: **nothing**. If any service is running on the host, this scenario proved the host, not the
containers.

---

## Scenario 6 — Started too early, recovers anyway *(FR-009, SC-006)*

```bash
docker compose -f docker-compose.yml -f docker-compose.app.yml down
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d   # everything at once
```

**Expect**: every service healthy without a manual restart. Ten times out of ten — run it more than
once, because this is a race and a single pass proves little.

**Failure looks like**: a service that stays down after its database comes up. `depends_on` waits for
the container's health check, not for readiness under load; the connection retry is the second layer
and this is what tests it.

---

## Scenario 7 — A token from one container works in another *(constitution IV)*

```bash
TOKEN=$(curl -s -X POST http://localhost:5056/api/auth/login \
  -H 'Content-Type: application/json' -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" \
  | python -c "import sys,json; print(json.load(sys.stdin)['token'])")

curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5057/api/categories \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"name":"x"}'
```

**Expect**: anything other than 401. A 400 is fine — it means authorization passed and validation
rejected the body.

**A 401 here is a configuration failure, not an authorization one**: `JWT_SECRET` differs between the
Identity container and the Catalog container. It is called out separately because it looks exactly
like a permissions bug and this project has already lost time to a signing-side/validation-side
mismatch once.

---

## Scenario 8 — A missing setting fails loudly *(FR-010, SC-007)*

Start one service with a required variable removed.

**Expect**: the container exits, and its **last log line names the missing variable**.

**Failure looks like**: the service starts and then fails every request, or starts with a silent
fallback to a default that happens to work locally. Both turn a configuration mistake into a runtime
mystery.

---

## Scenario 9 — The old way still works *(FR-012, SC-008)*

```bash
docker compose -f docker-compose.yml -f docker-compose.app.yml down
docker compose up -d          # infrastructure only, as before
./start-dev.sh
```

**Expect**: exactly what it did before this feature — six migrations applied, seven services on
5000 and 5056–5061.

**This is the scenario most likely to be skipped and most likely to be broken.** Every default in this
feature exists to keep it true, and it is the one a contributor who has not adopted containers will
hit first.

Also check the precedence change deliberately, because it affects them too:

```bash
PAYMENT_OUTCOME=Reject dotnet run --project src/Services/Payment/Ecommerce.Payment.WebApi/ --no-launch-profile &
sleep 12 && curl -s http://localhost:5061/health | grep -o '"configuredOutcome":"[^"]*"'
```

**Expect**: `"configuredOutcome":"Rejected"`. Before this feature it reported `Approved`, because the
`.env` file overrode the exported variable — the trap recorded in `CLAUDE.md`. This is the behaviour
change, and it is deliberate.

---

## What passing all nine does not prove

**Nothing here runs in CI.** Every scenario is a thing somebody has to run by hand, so a regression in
the container path will not be caught automatically — the same gap features 003 and 004 left open, one
level up. The one exception worth automating is scenario 1: an image that leaks a credential is the
failure that cannot be undone later, and it is a single command. That belongs in the task list, not in
this document.

**Nor does any of this prove the images are deployable anywhere**, only that they run. Tagging,
publishing and rolling back are
[#8](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/8).
