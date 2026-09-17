# Configuration Contract: Run the System in Containers

**Feature**: [spec.md](../spec.md) | **Date**: 2026-09-17

The configuration surface **is** the contract for this feature. An image has no settings of its own;
everything that makes one running copy different from another arrives through these variables.

Enumerated from the code, not from `.env.example` — which is known to be incomplete (`CLAUDE.md`
records that the `*_DB_PORT` variables are missing from it):

```bash
$ grep -oE 'GetEnvironmentVariable\("[A-Z_]+"\)' <service>/Program.cs | sort -u
```

---

## Read by every service

| Variable | Default today | Purpose |
| :--- | :--- | :--- |
| `DB_USER` | `postgres` | Database user |
| `DB_PASSWORD` | `123456` | Database password |
| `JWT_SECRET` | from `appsettings.json` | **Must be identical across all seven.** Identity signs with it; everyone else validates with it |

**`JWT_SECRET` is the variable most likely to produce a confusing failure.** If it differs between two
containers, a token Identity issues is rejected by the service that receives it, and the caller sees a
401 that looks like an authorization defect rather than a configuration one. Compose supplies it to
every service from a single source, and a quickstart scenario asserts cross-service acceptance
specifically.

## New in this feature, read by every service

| Variable | Default | Purpose |
| :--- | :--- | :--- |
| `DB_HOST` | `localhost` | Where the database is. **The gap this feature closes** — every connection string hardcoded `localhost`, while `RABBITMQ_HOST` was already environment-driven |
| `ASPNETCORE_URLS` | unset → today's pinned address | What the service listens on. Unset keeps `start-dev.sh` working exactly as before |
| `RUN_MIGRATIONS_ON_STARTUP` | unset (off) | When `true`, the service applies its own migrations at startup. Set **only** in the compose overlay — see research D3 |

## Per service

| Service | Database name | Host port | Its own variables |
| :--- | :--- | :--- | :--- |
| Identity | `IDENTITY_DB_NAME` | `IDENTITY_DB_PORT` (5435) | `ADMIN_EMAIL`, `ADMIN_PASSWORD` |
| Catalog | `CATALOG_DB_NAME` | `CATALOG_DB_PORT` (5433) | — |
| Order | `ORDER_DB_NAME` | `ORDER_DB_PORT` (5434) | — |
| Inventory | `INVENTORY_DB_NAME` | `INVENTORY_DB_PORT` (5437) | `INVENTORY_RESERVATION_TTL_MINUTES`, `INVENTORY_SWEEP_INTERVAL_SECONDS` |
| Payment | `PAYMENT_DB_NAME` | `PAYMENT_DB_PORT` (5438) | `PAYMENT_OUTCOME` |
| Orchestrator | `SAGA_DB_NAME` | `ORCHESTRATOR_DB_PORT` (5436) | — |
| Gateway | — | — | none today; gains cluster overrides below |

Note the inconsistency, which this feature does **not** fix: Orchestrator's database name variable is
`SAGA_DB_NAME` while its port variable is `ORCHESTRATOR_DB_PORT`. Renaming it is a breaking change to
anyone's local `.env` for no benefit to this feature. Recorded so the next reader knows it is known.

## Message broker — already environment-driven

| Variable | Default | Note |
| :--- | :--- | :--- |
| `RABBITMQ_HOST` | `localhost` | Read by all six services that publish or consume |
| `RABBITMQ_USER` | `guest` | |
| `RABBITMQ_PASS` | `guest` | **`RABBITMQ_PASS`, not `RABBITMQ_PASSWORD`** |

The services read `RABBITMQ_PASS`; `.env.example` and `docker-compose.yml` use `RABBITMQ_PASSWORD`. A
non-default broker password needs **both** names set. This is a pre-existing trap recorded in
`CLAUDE.md`, and the compose overlay must set both or it will be the first thing to break.

## Gateway cluster destinations — new, compose only

The gateway hardcodes `http://localhost:50XX` for all five clusters in `appsettings.json`. Inside
containers those addresses are wrong, and ASP.NET Core's configuration binding reaches them without
any code change:

```text
ReverseProxy__Clusters__identity-cluster__Destinations__destination1__Address=http://identity:8080/
ReverseProxy__Clusters__catalog-cluster__Destinations__destination1__Address=http://catalog:8080/
ReverseProxy__Clusters__order-cluster__Destinations__destination1__Address=http://order:8080/
ReverseProxy__Clusters__inventory-cluster__Destinations__destination1__Address=http://inventory:8080/
ReverseProxy__Clusters__payment-cluster__Destinations__destination1__Address=http://payment:8080/
```

The destination key must match what is in `appsettings.json` — the override replaces a value at a
path, it does not add one. Verified rather than assumed: all five clusters use `destination1`.

```bash
$ python -c "import json; d=json.load(open('.../appsettings.json',encoding='utf-8-sig')); \
    [print(k,'->',list(v['Destinations'].keys())) for k,v in d['ReverseProxy']['Clusters'].items()]"
identity-cluster -> ['destination1']
catalog-cluster -> ['destination1']
order-cluster -> ['destination1']
inventory-cluster -> ['destination1']
payment-cluster -> ['destination1']
```

There is no orchestrator cluster, because the orchestrator has no controllers.

---

## Precedence — the behaviour this feature changes

**Today**: every `Program.cs` reads a `.env` file and calls `Environment.SetEnvironmentVariable`
unconditionally, so **the file wins over the real environment**.

**After**: the file fills in only what the environment has not set.

```text
environment variable  >  .env file  >  appsettings.json  >  hardcoded default
```

This changes behaviour for people who never touch a container. It is the correct precedence — and the
current behaviour is already documented in `CLAUDE.md` as a trap, because `PAYMENT_OUTCOME=Reject
dotnet run ...` being silently ignored cost time during both feature 003 and feature 004.

It is also what makes an image configurable at all: a `.env` that reached a layer would otherwise
override every setting injected at runtime, and the same image would behave identically everywhere
regardless of what it was told.

## Failure behaviour

| Situation | Required behaviour |
| :--- | :--- |
| A required variable is missing | Fail at startup, naming the variable (FR-010). Not: serve every request badly |
| The database is not yet accepting connections | Retry, recover without manual intervention (FR-009) |
| The broker is not yet up | Retry — MassTransit already does this |
| A `.env` file is present inside an image despite `.dockerignore` | The environment still wins, so the mistake degrades rather than silently taking over |
