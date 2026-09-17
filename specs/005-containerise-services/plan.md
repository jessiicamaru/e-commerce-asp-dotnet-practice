# Implementation Plan: Run the System in Containers

**Branch**: `005-containerise-services` | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-containerise-services/spec.md`, tracked as issues
[#6](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/6) and
[#7](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/7)

## Summary

Make each of the seven services buildable into an image that takes its configuration from the
environment, and make `docker compose up` bring the whole system — services and infrastructure —
to a healthy state from a fresh checkout. `./start-dev.sh` keeps working unchanged.

Four things shape the design beyond the obvious:

- **Nothing creates the schema.** No service calls `Database.Migrate()`; migrations are applied only
  by `start-dev.sh` shelling out to `dotnet ef` from the host. "One command from a fresh checkout"
  would otherwise produce seven healthy services and zero tables (research D3). The spec did not
  anticipate this.
- **The gateway hardcodes `http://localhost:50XX` for all five clusters.** It is the one service whose
  configuration genuinely differs between host and container, and it needs no code change to fix —
  ASP.NET Core's environment-variable override reaches into the YARP section already (research D4).
- **`.dockerignore` comes before the first Dockerfile, not with it.** It is the only part of this
  feature whose absence is a security problem, and the window in which the mistake is cheap closes the
  moment somebody runs a build (research D6).
- **The `.env` precedence fix changes behaviour for everyone**, including people who never touch a
  container. It is correct and it is the point, but it is the change most likely to surprise.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 — images from `mcr.microsoft.com/dotnet/sdk:10.0` (build) and
`mcr.microsoft.com/dotnet/aspnet:10.0` (runtime)

**Primary Dependencies**: no new NuGet packages. Docker Engine with Compose v2 becomes a tool
dependency for the container path; it is already required for the infrastructure

**Storage**: unchanged. Six PostgreSQL databases, same names, same host ports

**Testing**: no new test project. The guarantees here are about how the system runs, not what it
computes, so they are verified by running it — quickstart scenarios, plus one automated check that an
image contains no secret (research D7)

**Target Platform**: Linux containers on a Windows or Linux developer machine

**Project Type**: Existing seven-service backend. No new projects; changes are to `Program.cs` in each,
plus build and compose files at the repository root

**Performance Goals**: a fresh checkout reaches a healthy system in under 5 minutes including the first
build (SC-004). Layer caching is what makes the second build fast, and it is a design constraint on the
Dockerfile, not an optimisation

**Constraints**: the same image in every environment (FR-007), no secret in any layer (FR-006), and
`./start-dev.sh` unchanged in behaviour (FR-012). The third is the one that constrains the other two —
every default must remain today's local value

**Scale/Scope**: 7 `Program.cs` files, 1 Dockerfile, 1 `.dockerignore`, 1 compose overlay, 1 shared
`.env` precedence fix repeated seven times

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.0.0.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** No service gains access to another's data. Each keeps its own database and its own container; compose puts them on one network, which is the equivalent of them being on one developer's machine today. The gateway learns where services live, which it already knows — only the spelling changes |
| **II. Clean Architecture Layering** | **Pass.** Every change is in `Program.cs`, which is the composition root and the layer that owns transport and configuration by definition. No Domain, Application or Infrastructure file is touched |
| **III. Atomic Writes and Idempotent Messaging** | **Not engaged.** This feature adds no handler, publishes no message and changes no write. The one adjacent question — running migrations at startup — is a deployment concern, and research D3 keeps it out of the request path entirely |
| **IV. Identity Comes From the Token** | **Pass, and worth checking rather than assuming.** `JWT_SECRET` must reach every service identically, or tokens signed by Identity fail validation elsewhere. Compose supplies it to all seven from one source. A quickstart scenario asserts a token minted by Identity is accepted by Catalog, because a signing-key mismatch produces a 401 that looks like an authorization bug |
| **V. Evidence Over Assumption** | **Pass.** `docker build` succeeding is explicitly **not** accepted as evidence (FR-004's acceptance is a running container reaching a database). The no-secret guarantee is checked by inspecting layer history rather than the final filesystem, because `RUN rm` leaves the file in an earlier layer. The end-to-end scenario asserts `0` host processes rather than "it seems to work" |

**Post-Phase 1 re-check**: no violations. Complexity Tracking is empty.

One thing stated rather than buried: **this plan changes behaviour for developers who never adopt
containers.** Fixing `.env` precedence (FR-003) means a value exported in a shell now beats the file,
where today the file wins. That is the correct precedence and the surprising one is what exists now —
it cost time during feature 003 and again during 004 — but somebody will hit it, and it belongs in
`CLAUDE.md` rather than in a commit message nobody re-reads.

## Project Structure

### Documentation (this feature)

```text
specs/005-containerise-services/
├── plan.md              # This file
├── spec.md              # 3 user stories, 13 requirements, 8 success criteria
├── research.md          # Phase 0: seven decisions with rejected alternatives
├── data-model.md        # Phase 1: the configuration surface (no entities)
├── quickstart.md        # Phase 1: validation scenarios
├── checklists/
│   └── requirements.md  # Spec quality checklist (all items pass)
├── contracts/
│   └── configuration.md # Every variable each service reads, and its default
└── tasks.md             # Phase 2 — created by /speckit-tasks
```

### Source Code (repository root)

```text
.dockerignore                        # NEW — first, before any Dockerfile
server/
├── Dockerfile                       # NEW — one file, parameterised by PROJECT
├── docker-compose.yml               # infrastructure, unchanged
├── docker-compose.app.yml           # NEW — the seven services
├── .env.example                     # + the new variables
├── start-dev.sh / start-dev.ps1     # unchanged
└── src/
    ├── ApiGateway/.../Program.cs    # listen address from config
    └── Services/*/…/Program.cs      # x6: db host from config, listen address
                                     #     from config, .env falls back
```

**Structure Decision**: **One Dockerfile, not seven.** The seven WebApi projects differ only in their
path; a single file taking a `PROJECT` build argument cannot drift out of sync, and compose names the
project per service. The rejected alternative — seven near-identical files — is more readable in
isolation and worse in aggregate, because a fix applied to six of seven is invisible.

The compose split is deliberate: `docker-compose.yml` keeps doing exactly what it does today, and the
services live in an overlay. `docker compose up` alone still gives an infrastructure-only environment
for `start-dev.sh`, and `docker compose -f docker-compose.yml -f docker-compose.app.yml up` gives the
whole system. Merging them into one file would force container users and script users down the same
path, which FR-012 forbids.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature finishes, and what it does not

**Finishes**: the system stops being tied to one machine. There is an artifact, it takes its settings
from outside, and the whole thing runs from a fresh checkout with one command.

**Does not**: put that artifact anywhere. Nothing is tagged, pushed, versioned or rollable-back-to —
that is [#8](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/8), and it is
pointless before this lands. Nor does it make schema changes survive a rollback
([#9](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/9)), introduce a secret
manager, or address running on more than one machine. The `PAYMENT_OUTCOME` stub is still a stub.
