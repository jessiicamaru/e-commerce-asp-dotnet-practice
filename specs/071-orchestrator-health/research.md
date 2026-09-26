# Research: The Orchestrator answers /health

> Written on 2026-09-27, after the feature merged (#155), from the code at that merge, the pull request and
> docs/architecture/microservices-design.md.

**Feature**: [spec.md](spec.md) | **Date of the decisions**: 2026-09-26

Who took each decision is not recorded beyond the pull request.

> An alternative marked *(reconstructed)* was not recorded when the decision was taken; it is the obvious other
> choice, named in the backfill so the decision reads whole, and its reason for rejection is inferred from the code
> and the recorded rationale.

---

## D1 - What `/health` checks

**Decision**: The saga database, through `AddDbContextCheck<OrchestratorDbContext>("orchestrator_postgres_db")`, plus
MassTransit's own `masstransit-bus` check, which MassTransit registers once health checks exist. The response writer is
the same JSON shape as the other services' (`status`, `service`, `checks[name, status, description, duration]`).

**Rationale**: Those are the two things the saga cannot move without, and the same two every messaging service
reports. The same JSON means the same tooling reads it.

**Alternatives considered**:

- *(reconstructed)* **A bare liveness endpoint returning 200.** Rejected: it would report healthy with the broker down, which is exactly
  the "Bus started" false comfort Principle V names.
- *(reconstructed)* **Add a controller.** Rejected: the Orchestrator has none on purpose; `MapHealthChecks` needs none.

---

## D2 - Route only the health check through the gateway

**Decision**: A new `orchestrator-cluster` and one route, `/api/orchestrator/health` → `/health`, with the compose
command-line override `--ReverseProxy:Clusters:orchestrator-cluster:Destinations:destination1:Address=http://orchestrator:8080/`.

**Rationale**: Every service's health is reachable through the gateway at `/api/<svc>/health`; the Orchestrator serves
nothing else, so nothing else is routed. The address is overridden on the command line because environment variables
do not bind YARP destinations in this setup (CLAUDE.md, "Running in containers").

**Alternatives considered**:

- *(reconstructed)* **A catch-all `/api/orchestrator/**` route.** Rejected: there is nothing else to reach, and a catch-all would expose
  whatever is added later without a decision.

---

## D3 - How it is tested

**Decision**: `HealthTests` hosts the real `Program` with `WebApplicationFactory`, swaps the broker for
`AddMassTransitTestHarness()` (which keeps a `masstransit-bus` check, in memory), removes the hosted services, and
points the DbContext at a real PostgreSQL on 5436 or at an unreachable port with a 2-second timeout.

**Rationale**: The database check is the real one against a real database, so the 200 means something; the 503 case is
the acceptance's other half. The sweeper is removed because it would read a saga table the plain `postgres` database
does not have. The plan allowed falling back to CI plus a live check if the host could not run without RabbitMQ; it
could.

**Alternatives considered**:

- **Only the live check.** Rejected once the harness made the host testable: a test runs on every push.

---

## D4 - Make everything wait for it

**Decision**: Compose puts the Orchestrator back on the shared `service-healthcheck` anchor and the gateway
`depends_on` it being healthy; CI's saga job adds `orchestrator:5058` to its readiness loop; `verify-saga.sh` probes it
at the start and reports its `/health` code in the stall message.

**Rationale**: A probe nobody waits on changes nothing. The stall message used to say the Orchestrator "has no
/health endpoint"; now it can say what it answers.

**Alternatives considered**:

- *(reconstructed)* **Leave compose's check disabled.** Rejected: the reason for disabling it ("no /health") is gone.
