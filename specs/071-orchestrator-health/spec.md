# Feature Specification: The Orchestrator answers /health

> Completed on 2026-09-27, after the feature merged (#155), from the code at that merge, the pull request and
> docs/architecture/microservices-design.md.

**Feature Branch**: `071-orchestrator-health` | **Created**: 2026-09-26 | **Issue**: #115 (closes it)

**Status**: Merged (#155, 2026-09-26).

## Why

Every service answers `/health` except the Orchestrator. It has no controllers, so `GET :5058/health` is a 404.
Compose disables its health check, and CI's saga job starts it without waiting for it. When an order sits in
`Submitted`, the Orchestrator is usually the cause, and it is the one service nothing can probe. The
constitution says every service exposes `/health`; CLAUDE.md recorded the disagreement as unresolved.

## User Scenarios

### US1 - An operator can tell whether the saga can run (P1)

An operator, a compose health check or CI asks the Orchestrator whether it is up, and learns whether its saga database
and its broker connection are healthy.

**Why this priority**: It is the whole feature. A stalled order is usually the Orchestrator, and until now it was the
one service nothing could ask.

**Independent Test**: `GET /api/orchestrator/health` through the gateway with the Orchestrator up (200, both checks
named), stopped (not 200), restarted (200 again).

**Acceptance Scenarios**:

1. **Given** the Orchestrator running with its database and broker reachable, **When** `/health` is asked, **Then** 200
   with `orchestrator_postgres_db` and `masstransit-bus` both `Healthy`.
2. **Given** its database unreachable, **When** `/health` is asked, **Then** 503.
3. **Given** the Orchestrator stopped, **When** `/api/orchestrator/health` is asked through the gateway, **Then** the
   answer is not 200 (the gateway answered 502 in the PR's run).

### US2 - Nothing starts ahead of the Orchestrator (P2)

Compose, CI and `verify-saga.sh` wait for the Orchestrator's health the way they wait for every other service.

**Why this priority**: The probe is the prerequisite; waiting on it is what turns it into fewer mystery stalls.

**Independent Test**: `docker compose ... up` reports the Orchestrator `healthy` and starts the gateway after it; CI's
saga job waits for seven services.

**Acceptance Scenarios**:

1. **Given** the compose stack starting, **When** the Orchestrator is not yet healthy, **Then** the gateway waits.
2. **Given** CI's saga job, **When** it probes readiness, **Then** the Orchestrator is among the seven services.
3. **Given** an order that stalls in `verify-saga.sh`, **When** it fails, **Then** the message reports the
   Orchestrator's `/health` code rather than saying it cannot be checked.

## Requirements

- **FR-001** `GET /health` reports the saga database and the broker connection, as the other services do: the
  DbContext check plus MassTransit's own `masstransit-bus` check. It is 200 when both are healthy and 503
  otherwise.
- **FR-002** The gateway routes `/api/orchestrator/health` to it. That needs an `orchestrator-cluster`, which the
  gateway has never had, and the compose command-line override for its address.
- **FR-003** Compose re-enables the Orchestrator's health check, and the gateway waits for it.
- **FR-004** CI's saga job waits for the Orchestrator's `/health` with the rest, and `verify-saga.sh` names its
  state when an order stalls.

## Acceptance

- `/api/orchestrator/health` is 200 through the gateway with the Orchestrator up, and not 200 when it is
  stopped.
- The saga job waits for all seven services.

### Edge Cases

- **The broker is down but the database is up**: FR-001 says 503. What `masstransit-bus` reports in that case is
  MassTransit's own behaviour; no test or live run of this case is recorded (the tests swap in the in-memory harness,
  and the live "stopped" run stopped the whole Orchestrator).
- **Only the health route reaches the Orchestrator** through the gateway; it serves nothing else.

### Key Entities

None - the feature adds an endpoint over existing dependencies.

## Success Criteria

- **SC-001**: `/health` is 200 naming both checks when healthy and 503 with the database unreachable - `HealthTests`,
  2 tests; dropping the database check fails both.
- **SC-002**: Live through the gateway: 200 up, 502 stopped, 200 restarted with docker reporting the container
  `healthy`.
- **SC-003**: `verify-saga.sh` passes against the stack with the Orchestrator probed (`approve=pass`).

## Assumptions

- MassTransit registers its `masstransit-bus` health check itself once health checks are added, as it does in
  Inventory.
- The response JSON can match the other services' shape.

## Out of scope

- Any Orchestrator endpoint other than `/health`.
- Readiness versus liveness as separate endpoints.
