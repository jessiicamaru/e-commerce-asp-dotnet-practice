<!--
SYNC IMPACT REPORT
==================
Version change: none (unfilled template) → 1.0.0
Bump rationale: MAJOR. First ratified version. The file previously contained only
placeholder tokens, so every principle here is newly defined rather than amended.

Modified principles: none (no prior principles existed)

Added sections:
  - Core Principles I-V
  - Technology & Implementation Constraints
  - Development Workflow & Quality Gates
  - Governance

Removed sections: none

Principles derived from: conventions the codebase demonstrably follows, plus three
defects already committed and fixed in this repository's history — outbox publish
ordering (commits 140fb39, ca5bc20), user id taken from the request body (725a700),
and a role claim validated against an assumption rather than a real token (c0c10b4).

Templates requiring updates:
  ✅ .specify/templates/plan-template.md      — no change needed; its Constitution
                                                Check references this file dynamically
  ✅ .specify/templates/spec-template.md      — no change needed; adds no mandatory
                                                sections or constraints
  ✅ .specify/templates/tasks-template.md     — no change needed; principle-driven
                                                checks fit the existing Polish phase
  ✅ .claude/skills/speckit-*/SKILL.md        — verified; all reference the
                                                constitution generically, no stale
                                                agent-specific naming
  ✅ specs/001-inventory-reservations/plan.md — Constitution Check rewritten; it
                                                previously recorded a vacuous pass
  ✅ CLAUDE.md                                — points at this file as the authority

Deferred TODOs: none
-->

# E-Commerce Microservices Constitution

## Core Principles

### I. Service Autonomy

Each service owns its data exclusively. No service reads or writes another service's
database, under any circumstance, including for a "quick" report or migration.

Exactly one service owns any given fact. Where a value is duplicated elsewhere for
display, the non-owning copy MUST NOT inform any decision — most importantly, a
sell/no-sell, allow/deny, or charge/refuse decision.

The only code shared across service boundaries is `Ecommerce.Contracts` (pure message
records, zero dependencies) and `Ecommerce.Shared` (cross-cutting infrastructure).
Services communicate through messages, never by calling into each other's internals.

**Rationale**: Two sources of truth do not stay in agreement; they diverge silently and
the disagreement surfaces as a customer-visible error long after the cause. Restricting
shared code to contracts is what makes a service replaceable.

### II. Clean Architecture Layering

Dependencies point inward, without exception:

- **Domain** has zero project or framework dependencies.
- **Application** depends on Domain and on abstractions only — `MassTransit.Abstractions`,
  never a transport package. It declares the interfaces it needs under
  `Common/Interfaces/`.
- **Infrastructure** implements those interfaces and registers them in its own
  `DependencyInjection.cs`.
- **WebApi** wires the above and owns transport concerns: controllers, consumers, bus
  configuration.

Features are foldered by use case (`<Aggregate>/Commands/<UseCase>/`,
`<Aggregate>/Queries/<UseCase>/`). Controllers contain no logic beyond dispatching
through MediatR.

**Rationale**: The layering exists so the Application layer can be exercised without a
database or a broker. A single leaked dependency removes that property for the whole
service.

### III. Atomic Writes and Idempotent Messaging (NON-NEGOTIABLE)

An entity change and the events it causes MUST commit as one unit: stage the entity,
publish through `IPublishEndpoint`, then call `SaveChangesAsync` **exactly once**.
Publishing after `SaveChangesAsync` is a defect, not a style preference — it permits an
event that describes a change the database never accepted, and a change no one is told
about.

Every consumer MUST be safe to process the same message twice. The broker redelivers;
this is normal operation, not an error condition. Idempotency MUST be enforced by a
database constraint or a guarded state transition, not by configuration alone.

State transitions MUST be written so that a repeated attempt affects zero rows rather
than applying the effect a second time.

**Rationale**: This repository has already shipped and fixed this exact ordering bug
twice. The failure is invisible in testing and only appears as data that cannot be
reconciled.

### IV. Identity Comes From the Token

Every service exposing protected endpoints MUST validate access tokens through
`Ecommerce.Shared`'s `AddJwtAuthentication`. Issuing tokens without validating them is
the same as having no authentication.

The caller's identity MUST be read from the validated token via `ICurrentUser`. It MUST
NOT be read from a request body, query string, or client-supplied header. A command
object MUST NOT carry a user id field.

Endpoints are authenticated by default; anonymous access is declared explicitly with
`[AllowAnonymous]`. Elevated privilege is granted out of band — never by
self-registration, and never by a code path a caller can reach.

**Rationale**: `SubmitOrderCommand` once took `UserId` from the request body, which let
any caller place an order on behalf of any user. The rule is absolute because the
mistake looks completely ordinary in review.

### V. Evidence Over Assumption

A change is "working" only when the real path has been exercised and the output shown.
A successful compile is not evidence. A log line reporting that a subsystem started is
not evidence that it connected.

A check MUST exercise the real dependency for the property it claims to verify.
Verifying behaviour against a fixture built from your own assumptions proves only that
you are self-consistent. Where the guarantee under test belongs to the database — row
locking, unique constraints, transaction boundaries — the test MUST run against a real
database.

When something cannot be verified, say so plainly and name what is unverified. Silence
reads as confirmation.

**Rationale**: Two incidents, both of which passed their checks. A hand-signed test token
was validated against the wrong claim name and passed, while every real token was
rejected with 403. Separately, `Bus started` was logged by a service that had never
reached a broker.

## Technology & Implementation Constraints

**Stack**: .NET 10, PostgreSQL 16, RabbitMQ via MassTransit 8.3.6, MediatR 12.4.1,
FluentValidation 12.1.1, EF Core with Npgsql, YARP at the gateway. Package versions stay
aligned across services; a version skew between MassTransit or MediatR packages is
treated as a defect.

**Persistence**: Primary keys are `Guid` generated with `Guid.CreateVersion7()` per
[ADR-001](../../docs/architecture/adr-001-uuidv7-primary-keys.md). Mapping is Fluent API
only, through `IEntityTypeConfiguration<T>`. Tables are snake_case plural; money is
`decimal(18,2)`; enums persist as strings. Invariants that matter MUST be expressed as
database constraints as well as in code.

**Errors**: All failures surface as RFC 7807 `ProblemDetails` through
`Ecommerce.Shared.Middlewares.GlobalExceptionHandler`. Internal messages are masked
outside Development.

**Configuration**: Secrets come from the environment. They are never committed, never
placed in `appsettings.json`, and never written into a workflow file. A service that
cannot find a required secret MUST fail at startup rather than failing every request.

**Service topology**: Each service pins its own HTTP port in `app.Run(...)`, owns a
database on its own host port, exposes `/health`, and is reachable through a gateway
route. Host ports MUST avoid those commonly held by natively installed software —
notably `5432` (PostgreSQL) and `5672` (RabbitMQ) — because a local install shadows a
container silently and the divergence only appears somewhere else, such as CI.

## Development Workflow & Quality Gates

**Commits**: Conventional Commits with a scope (`feat(order):`, `fix(outbox):`, `docs:`).
Commit directly to `main`; this project does not use feature branches. The message
explains why the change was needed, not only what changed.

**CI**: The build and the auth smoke test MUST pass. A red `main` is repaired before new
work starts. A check that cannot express the property it is meant to guard MUST be
replaced, not weakened.

**Tests**: New behaviour that cannot be verified by hand requires an automated check.
Concurrency, idempotency, and authorization boundaries fall in this category by
definition.

**Documentation**: Docs live under `docs/` and are updated in the same change as the code
they describe. A decision with lasting consequence is recorded with its rejected
alternatives, either as an ADR or in a feature's `research.md`.

**Design**: Features of material size go through the Spec Kit flow — specify, plan,
tasks, implement. The plan's Constitution Check is evaluated against this file, not
against habit.

## Governance

This constitution supersedes convention, precedent, and habit. Where the codebase and
this document disagree, either the code is wrong or the document must be amended
deliberately — the disagreement is never resolved by ignoring it.

**Amendment procedure**: Amend through `/speckit-constitution`. Every amendment records a
Sync Impact Report at the top of this file listing the version change, modified
principles, and the dependent artifacts checked. Dependent templates, skills, and runtime
guidance MUST be reviewed in the same change.

**Versioning policy**:

- **MAJOR** — a principle is removed or redefined in a way that invalidates existing
  designs.
- **MINOR** — a principle or section is added, or existing guidance is materially
  expanded.
- **PATCH** — clarification, wording, or correction that does not change meaning.

**Compliance**: Every `/speckit-plan` evaluates its design against these principles. A
violation MUST appear in that plan's Complexity Tracking with a concrete justification
and the simpler alternative that was rejected and why. A violation that cannot be
justified is a design that changes, not a gate that is waived. Principle III's
NON-NEGOTIABLE marking means it admits no Complexity Tracking entry at all.

**Runtime guidance**: [CLAUDE.md](../../CLAUDE.md) carries the operational detail —
commands, service map, and the traps this codebase has already sprung.

**Version**: 1.0.0 | **Ratified**: 2026-09-16 | **Last Amended**: 2026-09-16
