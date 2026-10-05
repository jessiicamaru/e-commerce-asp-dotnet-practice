# Feature Specification: The report's top-down documents

**Feature Branch**: `docs/295-thesis-documents`
**Created**: 2026-10-06
**Status**: Draft
**Issue**: #295
**Input**: `docs/` is thorough feature by feature. A graduation thesis also needs a few documents that read from the
top: what the system is, how it is deployed, how well it works, and how to show it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A reader sees the whole system on one page (Priority: P1)

An examiner opens the architecture overview and sees, in one diagram:
- every service, both apps, the gateway, the broker, the databases and the observability stack;
- the production entrance;
- which calls are HTTP, which are gRPC and which are messages.

Each box links to where it is explained.

**Independent Test**: every component in compose and in the production overlay appears in the diagram, and every
gRPC edge in the code appears as an edge.

### User Story 2 - An operator goes from a bare server to a running shop (Priority: P1)

The deployment guide is a numbered walk: server, DNS, secrets, the first deploy, the first administrator, checking it,
rolling back, watching it. It links to the reference documents rather than repeating them, and it says what has not
been exercised yet.

**Independent Test**: every command and file it names exists in the repository.

### User Story 3 - The evaluation chapter has its numbers (Priority: P1)

The evaluation states what was measured, on what, and what it showed: tests and what they prove, the concurrency
guarantee under load, latency, resilience, security scanning, and the defects measurement found. Every number links to
the run that produced it.

**Independent Test**: every figure appears in a generated report, a CI log or a spec record's evidence.

### User Story 4 - A demonstration walks every role (Priority: P2)

A demo script: start and seed the stack, then go through what a shopper, a seller, a moderator and an administrator
each see and do, plus the operational views.

**Independent Test**: every route it names exists in the storefront's or the back office's router.

### Edge Cases

- **A number that changes** (test counts): it is dated, with where to read the current one.
- **What is not done** (a real server deploy, a real VNPay merchant, the broker drain #304): it is stated as such,
  never implied.

## Requirements *(mandatory)*

- **FR-001**: `docs/overview/architecture-overview.md`, `docs/guides/deployment.md`, `docs/testing/evaluation.md` and
  `docs/guides/demo-script.md`, all linked from `docs/README.md`.
- **FR-002**: Every number comes from a run recorded in the repository, and the document links to it.
- **FR-003**: Limitations and open issues are stated in each document they affect.

## Success Criteria *(mandatory)*

- **SC-001**: The four documents exist, are linked from the docs index, and every relative link in them resolves.
- **SC-002**: Spot check: each table's numbers match their source.

## Assumptions

- Mermaid renders on GitHub, where the report is read alongside the code.
