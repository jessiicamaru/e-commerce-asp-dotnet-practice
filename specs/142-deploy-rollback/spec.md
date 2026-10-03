# Feature Specification: Deploy and roll back from CI

**Feature Branch**: `feat/288-deploy-rollback`
**Created**: 2026-10-03
**Status**: Draft
**Issue**: #288
**Input**: "A merge publishes images; nothing puts them on a server, and 'roll back' is a manual guess."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Deploy a release by its name (Priority: P1)

The owner picks a published `sha-` tag and runs the deploy workflow. The server ends up running exactly those images.
Every service is healthy and the shop answers over HTTPS. The workflow reports which version is running.

**Why this priority**: without it, the images CI publishes run nowhere.

**Independent Test**: on a machine with Docker, deploy one published tag. Both apps answer over HTTPS, every
`/api/<svc>/health` answers 200 through the front door, and the release log names the tag.

**Acceptance Scenarios**:

1. **Given** a published tag, **When** it is deployed, **Then** every container runs that tag's image, all are healthy,
   the smoke checks pass, and the log records `deployed`.
2. **Given** a tag with any image missing from the registry, **When** it is deployed, **Then** it stops before touching
   the running stack and says which image is missing.
3. **Given** a malformed name (`main`, `latest`, `sha-xyz`), **When** it is deployed, **Then** it is refused, because
   only an immutable `sha-` tag may name something deployable.

---

### User Story 2 - Roll back to what ran before (Priority: P1)

The owner deploys `previous`, or an earlier tag by name. The server returns to the release that ran before.

**Why this priority**: a deploy that cannot be undone is a bet. Rolling back must not be a guess.

**Independent Test**: deploy A, then B, then `previous`. A runs again, healthy.

**Acceptance Scenarios**:

1. **Given** A then B were deployed, **When** `previous` is deployed, **Then** A runs and the log records it.
2. **Given** a release whose services do not come up healthy, or whose smoke checks fail, **When** it is deployed,
   **Then** the previous release is put back automatically, the log says so, and the run fails.

---

### User Story 3 - Know it works without a server (Priority: P2)

Every pull request runs the deploy as a dry run. It renders the production stack from the files a server would
receive, checks what the stack exposes, and prints every step it would take. It touches nothing.

**Why this priority**: the deploy is rare. A broken deploy discovered on the day it is needed is the worst time to find
it.

**Independent Test**: CI's `deploy-dry-run` job passes, and fails if the overlay publishes a port it should not.

**Acceptance Scenarios**:

1. **Given** a pull request, **When** CI runs, **Then** the dry run renders the overlay from the shipped files alone and
   asserts:
   - only Caddy publishes ports;
   - nothing is built;
   - every image of ours carries the release tag;
   - the development tools are off.
2. **Given** the workflow run with no `DEPLOY_*` secrets, **When** it is not a dry run, **Then** it says which secrets are
   missing and stops.

### Edge Cases

- **Two deploys at once**: the second waits for the first, both in the workflow and on the server.
- **`previous` with no earlier release**: refused, with that reason.
- **The rollback itself fails**: the run fails and says so loudly. Nothing pretends to be healthy.
- **A rollback across a schema change**: safe by the expand-then-contract rule (CLAUDE.md, the `schema-compatibility`
  job). The deploy does not migrate anything itself: services migrate on startup (`RUN_MIGRATIONS_ON_STARTUP`).
- **An unknown SSH host key**: refused. The host's key is a secret, `DEPLOY_KNOWN_HOSTS`, never trusted on first use.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A `deploy` workflow, run by hand, takes a release (`sha-` tag or `previous`) and a dry-run switch.
- **FR-002**: It ships the release's own compose files: the ones from the commit the tag was built from, never `main`'s.
- **FR-003**: On the server, a script checks the name, checks that every image exists, pulls, starts, waits for health,
  runs the smoke checks and records the outcome.
- **FR-004**: A failed deploy puts back the previous release automatically.
- **FR-005**: `previous` resolves from the server's own release log.
- **FR-006**: Missing secrets stop a real run with their names. A dry run needs none.
- **FR-007**: CI runs the dry run on every pull request, asserting what the production stack exposes.
- **FR-008**: One deploy at a time, in the workflow (`concurrency`) and on the server (a lock).
- **FR-009**: The compose project has one fixed name, so each release's directory drives the same containers and
  volumes.

### Key Entities

- **Release**: a `sha-` tag. Every image of ours carries it.
- **Release log**: `releases.log` on the server, one line per outcome: time, tag, `deployed` / `failed` /
  `rolled-back`.

## Success Criteria *(mandatory)*

- **SC-001**: On this machine, a deploy of one published release, then another, then `previous`, each ends healthy, and
  the right images run.
- **SC-002**: A deploy forced to fail puts the previous release back, and the log shows both lines.
- **SC-003**: CI's dry run passes. It fails when the overlay is made to publish a database port.

## Assumptions

- The server has Docker with Compose v2.24+, curl and bash, and an SSH user in the `docker` group.
- The env file is in place at `/etc/ecommerce/.env` (specs/141). The workflow never writes secrets to the server.
- The SSH hop is exercised for real only once the owner adds the secrets. Everything behind it is tested here.
