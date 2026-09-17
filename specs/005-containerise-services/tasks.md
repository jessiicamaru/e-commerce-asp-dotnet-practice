# Tasks: Run the System in Containers

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `005-containerise-services`

**Input**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**One automated check, not a test project.** The guarantees here are about how the system runs, not
what it computes, so they are verified by running it. The exception is T003 — an image that leaks a
credential is the one failure that cannot be undone afterwards, and checking it is a single command.

**Organization**: by user story. **Note the phase order**: US2 (no secrets) comes before US1, because
its guard must exist before the first build, not after it.

---

## Format

`- [ ] [TaskID] [P?] [Story?] Description with file path`

`[P]` = parallelizable (different file, no incomplete dependency).

---

## Phase 1: Setup — the guard, first

**Nothing in this feature may run a build before T001 is committed.**

- [X] T001 [US2] Create `.dockerignore` at the repository root with, at minimum: `**/.env*`, `**/bin/`, `**/obj/`, `.git/`, `**/TestResults/`, `specs/`, `docs/`. **This is the first task of the feature on purpose** — Docker does not read `.gitignore`, and `server/.env` holds a real `JWT_SECRET`, `DB_PASSWORD` and `ADMIN_PASSWORD` (research D6)
- [X] T002 [US2] Add `DB_HOST`, `ASPNETCORE_URLS` and `RUN_MIGRATIONS_ON_STARTUP` to `server/.env.example`, with the local defaults and a comment on each. Also add the `*_DB_PORT` variables, which `CLAUDE.md` records as missing from it
- [X] T003 [US2] Add `.github/scripts/verify-image-has-no-secrets.sh` — builds one service and asserts, via `docker save | tar -t` and `docker history --no-trunc`, that no `.env` and no credential value appears in **any layer**. Inspecting the running container's filesystem is the wrong check and must not be what this script does (research D7)

**Checkpoint**: the guard is committed. Builds may now happen.

---

## Phase 2: Foundational

**Blocks every user story.** These are the code changes that make a container possible at all.

- [X] T004 Change the `.env` loader in all seven `Program.cs` files to **fall back rather than override**: only call `Environment.SetEnvironmentVariable` when the variable is not already set. Identical edit seven times — `src/ApiGateway/Ecommerce.ApiGateway/`, and `src/Services/{Identity,Catalog,Order,Inventory,Payment,Orchestrator}/…WebApi/`. **This changes behaviour for people who never use a container**, and it is the point (FR-003)
- [X] T005 Replace the hardcoded `Host=localhost` in the six service connection strings with a `DB_HOST` variable defaulting to `localhost`: `Identity/…:69`, `Catalog/…:61`, `Inventory/…:75`, `Orchestrator/…:48`, `Order/…:60`, `Payment/…:69`. The default keeps `start-dev.sh` working (FR-001, FR-012)
- [X] T006 Replace `app.Run("http://localhost:PORT")` in all seven with the conditional from research D2 — honour `ASPNETCORE_URLS` when set, otherwise keep today's pinned address. **Do not simply drop the argument**: every service would then default to the same port and collide under `start-dev.sh`
- [X] T007 Add the gated startup migration to the six services that own a database — `RUN_MIGRATIONS_ON_STARTUP == "true"` only. Nothing creates the schema today (`grep -rn "Database.Migrate()" server/src` → no matches), so without this a container stack comes up healthy and empty (research D3)
- [X] T008 Add `EnableRetryOnFailure()` to the Npgsql configuration in each service's `AddInfrastructure`. `depends_on` waits for a container, not for readiness under load (research D5)
- [X] T009 Make each service fail at startup, naming the variable, when a required setting is missing (FR-010). At minimum `JWT_SECRET` and `DB_PASSWORD` — a service that starts and then rejects every request turns a configuration mistake into a runtime mystery

**Checkpoint**: `dotnet build` succeeds, `./start-dev.sh` still works, and nothing about containers has been tried yet.

---

## Phase 3: User Story 2 — The package contains no credentials (P1)

**Goal**: a built image, inspected, contains no secret.

**Independent test**: build one image, inspect its layers, find nothing.

- [X] T010 [US2] Create `server/Dockerfile` — multi-stage, `mcr.microsoft.com/dotnet/sdk:10.0` to publish and `mcr.microsoft.com/dotnet/aspnet:10.0` to run, taking a `PROJECT` build argument. **Copy every `.csproj` and restore before copying the source**, or a one-line code change re-downloads every package and SC-004's five minutes goes on the first service (research D1)
- [X] T011 [US2] Run as a non-root user and `EXPOSE 8080` in the same Dockerfile (FR-013)
- [X] T012 [US2] Run T003's script against a freshly built image and **paste the real output**. If it fails, the credentials in `server/.env` must be rotated rather than the image rebuilt — an image built once may already have been shared

**Checkpoint**: quickstart scenario 1 passes. An image exists and is safe to share.

---

## Phase 4: User Story 1 — A service runs somewhere else (P1)

**Goal**: one packaged service, configured from outside, reaching a database.

**Independent test**: quickstart scenarios 2 and 3 — one service against a database, then the same
image against a different one.

- [X] T013 [US1] Run quickstart scenario 2: one Catalog container, one Postgres container, `DB_HOST` supplied from outside, `/health` answered from the host. **A successful `docker build` is not evidence of this** (constitution V)
- [X] T014 [US1] Run quickstart scenario 3 — the **same image** against a second database, proving it is not tied to one deployment (SC-002). This is the scenario that distinguishes a configurable image from one that happens to work
- [X] T015 [US1] Run quickstart scenario 8: a service started with a required variable missing exits, and its last log line names it. Fix T009 if it does not

**Checkpoint**: the image is genuinely configurable. Nothing about the full system yet.

---

## Phase 5: User Story 3 — The whole system, one command (P2)

**Goal**: `docker compose up` brings up everything, and a checkout completes inside it.

**Independent test**: quickstart scenarios 4 through 7.

- [X] T016 [US3] Create `server/docker-compose.app.yml` with the seven services, each built from `server/Dockerfile` with its own `PROJECT` argument, each mapping container 8080 to today's host port (5000, 5056–5061). **An overlay, not an edit to `docker-compose.yml`** — `docker compose up` alone must still give infrastructure only, or `start-dev.sh` users are forced down the container path (FR-012)
- [X] T017 [US3] Wire configuration in the overlay per [contracts/configuration.md](./contracts/configuration.md): `DB_HOST` naming each Postgres service, `RABBITMQ_HOST`, `ASPNETCORE_URLS=http://+:8080`, `RUN_MIGRATIONS_ON_STARTUP=true`, and **`JWT_SECRET` from one source for all seven** — a mismatch produces a 401 that looks like an authorization defect
- [X] T018 [US3] Set **both** `RABBITMQ_PASS` and `RABBITMQ_PASSWORD` in the overlay. The services read the first, compose and `.env.example` use the second; `CLAUDE.md` records this trap and it will be the first thing to break
- [X] T019 [US3] Override the gateway's five YARP cluster destinations by environment variable, using the `ReverseProxy__Clusters__<name>__Destinations__destination1__Address` form. No code change — verified that all five clusters use the key `destination1` (contracts/configuration.md)
- [X] T020 [US3] Add `depends_on` with `condition: service_healthy` for each service's database and for RabbitMQ, using the health checks the infrastructure containers already define
- [X] T021 [US3] Add a health check to each service in the overlay, hitting its existing `/health` (FR-008)
- [X] T022 [US3] Run quickstart scenario 4 and record **both** timings — first build and second. If the second is not fast, T010's layer ordering is wrong and people will abandon this path
- [X] T023 [US3] Run quickstart scenario 5: place an order end to end through the gateway, and assert **0 `Ecommerce.*` processes on the host**. Without that assertion the scenario may have proved the host rather than the containers
- [X] T024 [US3] Run quickstart scenario 6 — everything started at once, no manual restart, **more than once**. It is a race, and a single pass proves little
- [X] T025 [US3] Run quickstart scenario 7: a token minted by the Identity container accepted by the Catalog container. A 401 here is `JWT_SECRET` differing, not a permissions bug

**Checkpoint**: all of #6 and #7 are demonstrable from a fresh checkout.

---

## Phase 6: Polish & Cross-Cutting

- [X] T026 Run quickstart scenario 9 — `./start-dev.sh` unchanged, **including the precedence check**: `PAYMENT_OUTCOME=Reject dotnet run …` must now report `configuredOutcome: Rejected` where it used to report `Approved`. **This is the task most likely to be skipped and the change most likely to surprise someone**
- [X] T027 [P] Wire T003's script into `.github/workflows/ci.yml` as a job that builds one image and asserts no secret. The only part of this feature worth automating — the failure it catches cannot be undone later
- [X] T028 [P] Update `CLAUDE.md`: the `.env` precedence Gotcha now describes the **new** behaviour, a container section, and the two new variables. The current entry documents the old trap and would become false
- [X] T029 [P] Update `docs/infrastructure/database-setup.md` and `docs/infrastructure/rabbitmq-setup.md` if they describe host-only access
- [X] T030 [P] Add a `docs/guides/` entry, or extend `troubleshooting.md`, with the container path — the one command, and what each quickstart failure means
- [X] T031 Run `dotnet build` and the full `dotnet test` with `DB_PASSWORD` set; report the real pass counts per project. This feature touches seven `Program.cs` files and should break none of the 59 existing tests

---

## Dependencies

```text
Phase 1 (T001-T003)  ← the guard, before any build
   └─> Phase 2 (T004-T009)  ← code changes; start-dev.sh must still work after this
          ├─> Phase 3 US2 (T010-T012)  ← the image exists and is safe
          │      └─> Phase 4 US1 (T013-T015)  ← it is configurable
          │             └─> Phase 5 US3 (T016-T025)  ← the system runs
          │                    └─> Phase 6 (T026-T031)
```

- **T001 blocks everything**, and not for a technical reason. The build works fine without it; the
  point is that the window in which the mistake is cheap closes the moment somebody runs one.
- **US2 precedes US1** in the phase order although both are P1, because T010 (the Dockerfile) cannot
  be written safely before T001 and is pointless before T004–T006.
- **T026 depends on nothing in Phase 5** and could be run right after Phase 2. Doing so is a good idea:
  it is the check that the foundational changes did not break the existing workflow, and finding that
  out after building the whole container stack is finding it out late.

## Parallel opportunities

| Phase | Can run together |
| :--- | :--- |
| 1 | T002, T003 (T001 first, alone) |
| 2 | T004, T005, T006 touch the same seven files — do them as one pass per file, not per task |
| 5 | T017–T021 are all the overlay; write it once, then T022–T025 in order |
| 6 | T027, T028, T029, T030 |

**Phase 2 deserves a note**: T004, T005, T006, T007, T008 and T009 all edit the same seven
`Program.cs` files. Treat them as one pass per file applying six changes, rather than six passes over
seven files. The task list is split by *concern* so nothing is forgotten, not by edit session.

## Implementation strategy

**MVP is Phase 1 + Phase 2 + Phase 3 + Phase 4** — 15 tasks. At that point an image exists, contains
no secret, and runs configured from outside, which closes #7 and most of #6. It is worth stopping
there and running quickstart scenario 9 before starting Phase 5, because Phase 2 changed behaviour for
everyone and the container stack is a poor place to discover that.

Then Phase 5, which is what makes any of it provable rather than plausible.

**Do not defer T012, T023 or T026.** T012 is the check that the guard worked; T023 is the assertion
that separates "the containers work" from "the host was still running"; T026 is the only thing
protecting the people who did not ask for any of this.


---

## What actually happened, against what this list assumed

Recorded here rather than only in commit messages, because three of these were wrong assumptions in
the plan rather than surprises in the code.

- **T001's `.dockerignore` belongs in `server/`, not the repository root.** Docker reads it from the
  build context, and the context is `server/` because that is where the solution file lives.
- **T020 assumed the infrastructure containers already defined health checks.** They did not - none
  of the six Postgres services or RabbitMQ had one. They were added to `docker-compose.yml`, which is
  additive and changes nothing for `start-dev.sh` users.
- **T021 assumed a health check could just call `/health`.** The `aspnet:10.0` image ships neither
  `curl` nor `wget`, and a container health check has to run *inside* the container. `curl` is now
  installed in the runtime stage.
- **T019's environment-variable override does not work.** The documented double-underscore form for
  YARP destinations did not bind; command-line arguments do. Both were verified. See research D4 for
  what was ruled out.
- **T003's first implementation passed on an image that provably leaked.** `docker save | tar -t`
  lists OCI blob digests, and layers are gzipped, so it was scanning the wrong level of nesting. The
  negative control is the only reason this was found; the script now reports how many layers it
  actually read, so a scan of nothing fails instead of passing.
- **T029 and T030 were marked done before they were done.** The docs under `docs/` were not touched
  in the implementation commits - only `CLAUDE.md` and `ci.yml` were. Caught afterwards and fixed in
  a follow-up commit, which added `docs/infrastructure/running-in-containers.md`, container traps as
  troubleshooting section 7, the host-vs-container port note in `database-setup.md`, the
  `RABBITMQ_PASS`/`RABBITMQ_PASSWORD` note in `rabbitmq-setup.md`, and index entries so the new
  pages are reachable. Ticking a box ahead of the work is how a checklist stops meaning anything.
- **SC-004's five-minute budget was missed on the first build: 7m03s.** The second build was 24s.
  The layer ordering works; the cold build on this machine simply takes longer than the spec assumed.
  Not fixed, and not hidden.
