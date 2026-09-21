# Implementation Plan: The Checkout Flow Is Verified End to End

**Branch**: `007-saga-e2e-verification` | **Date**: 2026-09-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-saga-e2e-verification/spec.md`

## Summary

The system's one cross-service promise — order placed, stock held, payment taken, order completed,
stock gone — has no automated check. Sixty-one tests exist and every one of them is confined to a
single service, which is why a defect *between* services once settled an order while its stock
stayed held with all fifteen of that service's tests green.

This adds `.github/scripts/verify-saga.sh`: a shell script that places a real order over HTTP with a
real signed token, follows it to a terminal state, and asserts on `QuantityOnHand` and
`QuantityReserved` — not only on the derived `QuantityAvailable`, which looked correct throughout
the motivating bug. It runs both the success and the compensation path, in a new CI job parallel to
`auth-smoke`, and `publish` is gated on it.

A shell script rather than a test project because the property under test — *six separate processes
on one real broker agree* — exists only between processes. An in-memory harness creates no queues,
so two consumers with the same class name are simply two consumers, and the bug is invisible.
([research D1](./research.md))

## Technical Context

**Language/Version**: Bash, with `python3` for JSON. No new .NET code.

**Primary Dependencies**: `curl`, `python3` (or `python`). **No `jq`** — absent on several machines
that run the existing scripts, which is why `verify-auth.sh` avoids it too.

**Storage**: None added. The check reads through HTTP and writes to no database, its own or anyone
else's.

**Testing**: The deliverable *is* a test. It is itself validated by two negative controls
([quickstart](./quickstart.md) scenarios 4 and 5), because an unfalsified check is indistinguishable
from an absent one and this repository has shipped that exact defect.

**Target Platform**: Ubuntu runners in CI; Git Bash on Windows and Linux locally, against
`./start-dev.*`.

**Project Type**: A verification script plus a CI job. No service changes.

**Performance Goals**: One order settles well inside a 60-second budget. Elapsed time is printed on
every success so the real distribution is visible rather than guessed at.

**Constraints**:

- Payment's outcome is fixed at startup (`IOptions`, resolved in the constructor), so **two
  scenarios require two Payment lifetimes**. Two instances at once would compete for one queue —
  the very failure being tested. ([research D3](./research.md))
- Stock registration is asynchronous: the row is created by a consumer reacting to
  `ProductCreatedEvent`, so setup must wait for it.
- Every quantity assertion is against a reading from the same run. Databases persist between runs.

**Scale/Scope**: One new script (~250 lines), one new CI job, two documentation updates. No
production code changes, by design — FR-014.

## Constitution Check

*GATE: evaluated against [constitution v1.1.0](../../.specify/memory/constitution.md).*

| Principle | Verdict | How this design satisfies it |
| :-- | :-- | :-- |
| **I. Service Autonomy** | **PASS** | The check reads and writes nothing but HTTP. It never touches a service's database — including to seed its own fixtures, which would have been faster and would have broken the rule the check exists to verify. Test data is created through the same public endpoints a customer and an administrator use. |
| **II. Clean Architecture Layering** | **PASS, not applicable** | No .NET code is added or changed, so there is no layer to violate. Worth stating rather than ticking silently. |
| **III. Atomic Writes and Idempotent Messaging (NON-NEGOTIABLE)** | **PASS** | Nothing here publishes, consumes, or writes. The design deliberately avoids subscribing to the broker to observe completion ([research D4](./research.md)) — adding a queue to a check whose purpose is catching queue mistakes would put the check inside the thing it measures. This feature *verifies* Principle III from outside: assertion A3 fails exactly when a confirmation was lost. |
| **IV. Identity Comes From the Token** | **PASS** | The order is placed with a real token obtained from Identity, and read back through the owner-scoped endpoint. `SubmitOrderCommand` has no `UserId` and this check does not ask for one. It exercises the identity path rather than going around it — one of only two places a real signed token reaches the order-read path. |
| **V. Evidence Over Assumption** | **PASS — this feature is an instance of it** | The whole feature exists because a green suite was taken as evidence of a guarantee it could not see. Design decisions were verified against the code, not assumed: `StubPaymentGateway` resolving its outcome in the constructor, the confirm path decrementing both `OnHand` and `Reserved`, the release path decrementing only `Reserved`, and `/health` reporting `Approved`/`Rejected` where the input is `Approve`/`Reject`. Each is cited where it is relied on. |

**Development Workflow gates**:

- *"New behaviour that cannot be verified by hand requires an automated check."* This feature is that
  clause being honoured, three features late.
- *"A check that cannot express the property it is meant to guard MUST be replaced, not weakened."*
  Hence the negative controls being tasks rather than intentions.

**No Complexity Tracking entries.** No principle is bent and nothing needs justifying. If that
changes during implementation, it belongs in the table below rather than in a commit message.

**Post-Phase-1 re-check**: unchanged. The design added a contract and a data model and neither
introduced a service dependency, a message, or a database write.

## Project Structure

### Documentation (this feature)

```text
specs/007-saga-e2e-verification/
├── plan.md                    # this file
├── spec.md                    # what and why
├── research.md                # 8 decisions + 3 things found while reading
├── data-model.md              # observable states and the assertions
├── quickstart.md              # 7 validation scenarios, 2 of them negative controls
├── contracts/
│   └── verify-saga.md         # the script's inputs, exit codes and promises
├── checklists/
│   └── requirements.md
└── tasks.md                   # produced by /speckit-tasks
```

### Source (repository root)

```text
.github/
├── scripts/
│   ├── verify-auth.sh              # existing - the shape this follows
│   ├── verify-image-has-no-secrets.sh
│   ├── check-schema-compatibility.sh
│   └── verify-saga.sh              # NEW
└── workflows/
    └── ci.yml                      # NEW job `saga-e2e`; `publish` gains it in `needs`

docs/
└── architecture/
    └── saga-orchestration-roadmap.md   # Phase 7's E2E half becomes done

CLAUDE.md                             # the commands section gains this check
```

**Structure Decision**: the script goes beside the three checks that already exist, in
`.github/scripts/`, and follows `verify-auth.sh`'s conventions exactly — `set -euo pipefail`, a
probed Python interpreter, `fail()` / `pass()` / `json_field()` / `json_object()` / `status()`, and
`::error::` annotations. A reader who knows one knows the other. **No `server/` change at all**: this
feature observes existing behaviour and FR-014 forbids adding a seam to make it observable.

### The CI job

A new `saga-e2e` job rather than an extension of `auth-smoke` ([research D2](./research.md)):

```text
build ──┬──► auth-smoke  ─────┐
        └──► saga-e2e   ─────┼──► publish   (push only)
                              │
             image-secrets ───┘   (PR only, needs: build)
             schema-compatibility (PR only)
```

Both smoke jobs depend only on `build`, so they run concurrently and the wall-clock cost is `max()`,
not `sum()`. The honest cost is a second checkout and build — roughly 1m40s of runner time, softened
by the NuGet cache. It buys a failure that names itself: merged into `auth-smoke`, a Payment startup
problem would turn the *authentication* check red, and this pipeline's history is full of failures
reported somewhere other than where they happened.

The job needs six databases and a broker. Four of the seven containers already exist in
`auth-smoke`'s definition — including `postgres-inventory`, which that job provisions and **never
uses** ([research, Found while reading](./research.md)).

It runs the script twice, restarting Payment with `PAYMENT_OUTCOME=Reject` in between, and sets
`SAGA_E2E_REQUIRE_ALL=1` so a service that failed to start is a failure rather than a quiet skip.

## Complexity Tracking

> No Constitution Check violations. This table is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
