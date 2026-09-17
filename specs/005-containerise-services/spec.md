# Feature Specification: Run the System in Containers

**Feature Branch**: `005-containerise-services`

**Created**: 2026-09-17

**Status**: Draft

**Input**: Issues [#6](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/6) — "services cannot run in a container" — and [#7](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/7) — "no service can be built into a deployable image"

## Why this exists

Every service in this system is written to run on one developer's own machine and nowhere else. Each
addresses its database as if it were on the same host, each claims a port on that host by name, and
each reads a local file of settings that overrides anything the surrounding environment tries to tell
it.

The result is that there is no artifact. Running the system somewhere else means copying the source
there and building it again, so what runs is never provably what was tested, and there is nothing to
go back to when a change turns out badly.

There is also a trap waiting. The settings file holds real credentials and is correctly kept out of
version control — but the tool that builds images uses a *different* exclusion list, which does not
exist here. Whoever writes the first image build will, by default, copy those credentials into it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A service runs somewhere that is not a developer's machine (Priority: P1)

An operator starts one service in an isolated environment, points it at a database, and it works —
without editing any file inside it.

**Why this priority**: Everything else depends on this. A packaged service that cannot be configured
from outside is a packaged service that can only ever run in the place it was built for.

**Independent test**: Start one service in isolation with its settings supplied from outside, and
confirm it reaches its database and reports itself healthy. No other service is required.

**Acceptance Scenarios**:

1. **Given** a packaged service and a database in a separate environment, **When** the service is
   started with settings naming that database, **Then** it connects to it and reports healthy.
2. **Given** the same package, **When** it is started with settings naming a *different* database,
   **Then** it uses that one instead — the package is not tied to a particular deployment.
3. **Given** a running packaged service, **When** something outside its environment calls its health
   endpoint, **Then** it answers.
4. **Given** settings supplied from the environment **and** a settings file present, **When** the two
   disagree, **Then** the environment wins.

---

### User Story 2 - The package contains no credentials (Priority: P1)

Whoever builds or receives a package can inspect it and find no secret in it.

**Why this priority**: Equal first with US1, and for a different reason. This is the only part of this
feature whose absence is a security problem rather than an inconvenience — and it is a problem that
becomes unfixable the moment a package is shared, because a credential that has been distributed must
be rotated, not deleted.

**Independent test**: Build a package, inspect its contents for the known credential values, and find
none.

**Acceptance Scenarios**:

1. **Given** a built package, **When** its contents are inspected, **Then** the settings file
   containing real credentials is absent.
2. **Given** a built package, **When** its full history of contents is inspected rather than only its
   final state, **Then** no credential appears at any point — removing a file after adding it does not
   remove it.
3. **Given** the build process, **When** it runs in a working copy that contains local secrets,
   **Then** those secrets are excluded automatically rather than by the person remembering.

---

### User Story 3 - The whole system runs together, in containers (Priority: P2)

Somebody with a fresh checkout starts everything with one command and can place an order that
completes, with nothing running directly on their machine.

**Why this priority**: This is what makes US1 provable rather than plausible. A single service reaching
a database proves connection strings; only the full system proves that services reach *each other*,
that the message broker works across environment boundaries, and that the gateway can still route.
It is second because it cannot start until US1 is done.

**Independent test**: On a machine with nothing but the checkout and a container runtime, bring
everything up and complete a checkout end to end.

**Acceptance Scenarios**:

1. **Given** a fresh checkout, **When** the system is brought up with one command, **Then** all seven
   services and all supporting infrastructure start and report healthy.
2. **Given** that running system, **When** a shopper places an order for stock that exists, **Then**
   the order completes, the stock is deducted, and the catalogue's availability updates — with nothing
   running directly on the host.
3. **Given** that running system, **When** a request is made through the gateway, **Then** it reaches
   the right service.
4. **Given** a developer who has not adopted containers, **When** they use the existing start script,
   **Then** it works exactly as before.

---

### Edge Cases

- **A service starts before its database is ready.** Containers start in parallel; a service that
  crashes because its database was three seconds behind has not failed, it was early.
- **A service starts before the message broker is ready.** Same problem, different dependency, and
  this one has bitten this project before in CI.
- **A required setting is missing entirely.** The service must fail at startup with a message naming
  what is missing, not serve every request badly.
- **Two environments on one machine.** Nothing should assume it owns a fixed port on the host.
- **A settings file is present inside the package despite everything.** The environment must still
  win, so that the mistake degrades rather than silently taking over.
- **Someone runs the existing start script after this change.** It must behave as it always did.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every service MUST take the location of its database from its environment, with the
  current local value as the default.
- **FR-002**: Every service MUST take the address it listens on from its environment, with the current
  behaviour as the default.
- **FR-003**: Settings supplied by the environment MUST take precedence over settings in a local file.
  A local file MUST only fill in what the environment has not set.
- **FR-004**: Each service MUST be buildable into a self-contained package that can run without the
  source code.
- **FR-005**: The build MUST exclude local settings, build output and version-control data
  automatically, without relying on the person running it to remember.
- **FR-006**: A built package MUST contain no credential, at any point in its contents, not merely in
  its final state.
- **FR-007**: A package MUST behave identically wherever it runs, differing only by the settings it is
  given.
- **FR-008**: A service MUST report itself healthy in a way its surrounding environment can check
  automatically.
- **FR-009**: A service that starts before something it depends on MUST recover once that dependency
  is available, rather than requiring a restart by hand.
- **FR-010**: A service missing a required setting MUST fail at startup, naming what is missing.
- **FR-011**: The whole system MUST be startable with one command from a fresh checkout, with no
  component running directly on the host.
- **FR-012**: The existing start script MUST continue to work unchanged for anyone not using
  containers.
- **FR-013**: A service MUST run with no more privilege than it needs.

### Key Entities

- **Service package**: A runnable, self-contained copy of one service. It has no settings of its own —
  it is told everything at start. The same package is what runs in every environment.
- **Environment settings**: The values that make one running copy of a package different from another —
  where its database is, where the broker is, what it listens on, and its credentials. Supplied from
  outside the package, never inside it.
- **Exclusion list**: What the build must never copy into a package. It is separate from the
  version-control exclusion list, and the two are not interchangeable.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: One packaged service, given a database address from its environment, reaches that
  database and reports healthy — 0 files edited inside the package.
- **SC-002**: The same package pointed at two different databases uses both, demonstrated by data
  appearing in the one named and not the other.
- **SC-003**: Inspecting a built package for the known credential values yields **0 matches**, across
  its full contents rather than its final state.
- **SC-004**: A fresh checkout reaches a fully healthy system in **one command** and under 5 minutes
  on a developer machine, including the initial build.
- **SC-005**: With everything in containers, an order placed through the gateway completes end to end,
  and **0 processes** belonging to this system are running directly on the host.
- **SC-006**: A service started before its database recovers without manual intervention, 10 times out
  of 10.
- **SC-007**: A service started with a required setting missing exits, and its final message names the
  missing setting.
- **SC-008**: The existing start script produces the same working system it did before this change.

## Assumptions

- **The whole system in containers is in scope** (the user's decision on 2026-09-17). The rejected
  alternatives were packaging alone — which proves connection strings but leaves service-to-service
  messaging, broker access and gateway routing unverified — and going further by deleting the existing
  start script, which contradicts FR-012 and changes how every contributor works day to day.
- **The existing start script survives.** Containers become *a* way to run the system, not the only
  way. This is explicit in the requirement and is the reason FR-001 and FR-002 keep today's values as
  defaults.
- **Services keep their current external identities.** Each remains reachable where it is reachable
  today, so the gateway configuration, the start script and the documented port table stay true. What
  a service listens on *inside* its own environment is an implementation concern and is not specified
  here.
- **No deployment target exists yet.** This feature makes packages and runs them locally. Publishing
  them to a registry is [#8](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/8)
  and deliberately not in scope.
- **Credentials for local use are still local.** This feature does not introduce a secret manager; it
  stops secrets being copied where they do not belong.
- **The database schema is unchanged.** Nothing here touches data.

## Dependencies

- The message broker address is **already** taken from the environment by every service that uses it —
  verified, not assumed. The database address is the one that was never given the same treatment, which
  is why FR-001 exists and no equivalent requirement is needed for the broker.
- Every service already exposes a health endpoint, so FR-008 is about making it *checkable
  automatically*, not about building one.

## Out of scope

- Publishing packages anywhere, tagging them, or rolling back to an earlier one (#8).
- Any rule about how schema changes interact with running an older package
  ([#9](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/9)).
- Orchestration beyond a single machine — no cluster, no scaling, no service mesh.
- Changing what any service does. This feature changes only where and how they run.
