# Feature Specification: The Orchestrator answers /health

**Feature Branch**: `071-orchestrator-health` | **Created**: 2026-09-26 | **Issue**: #115 (closes it)

## Why

Every service answers `/health` except the Orchestrator. It has no controllers, so `GET :5058/health` is a 404.
Compose disables its health check, and CI's saga job starts it without waiting for it. When an order sits in
`Submitted`, the Orchestrator is usually the cause, and it is the one service nothing can probe. The
constitution says every service exposes `/health`; CLAUDE.md recorded the disagreement as unresolved.

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
