# Feature Specification: Every audit action has words, and the status page lists every service

**Feature Branch**: `fix/244-staff-labels-status` | **Created**: 2026-10-01 | **Issue**: #244

**Status**: Draft

**Input**: Issue #244 - "staff screens show raw action names and an out-of-date status page" - found in the screen review of 2026-10-01.

## Why

Moderators judge a person by their history and administrators read the audit log to find out what happened; half the actions recorded since the log's labels were written read as code - `ShopClosed`, `TrackingCorrected`, `TwoFactorReset` - and in English for a Vietnamese reader. Nothing failed when an action was added without words, which is how 54 of them went unlabelled. The status page, meanwhile, said the orchestrator has no health endpoint (it has had one since specs/071) and left out Activity.

## User Scenarios & Testing *(mandatory)*

### US1 - Staff read every action in words (Priority: P1)



**Acceptance Scenarios**:

1. **Given** any action a service records in the audit log, **When** it shows on Moderation, the audit log or a person's history, **Then** it reads as words in the reader's language.
2. **Given** a developer records a new action without words for it, **Then** a client test fails naming the action.

---

### US2 - The status page shows every service (Priority: P2)



**Acceptance Scenarios**:

1. **Given** `/status`, **Then** every service the gateway has a health route for is listed, with a readable name, including Activity and the orchestrator, and no out-of-date note.
2. **Given** a health route added to the gateway, **Then** a client test fails until the page lists it.

### Edge Cases

- Some actions are built at run time - `Return{to}`, a ternary between two names, or a variable chosen from a switch. The test resolves literals and ternaries itself; a call whose action it cannot read must be declared with its values in the test, so a new indirect call fails until somebody says what it records.
- The labels describe the action, not the subject: the subject and summary are shown beside it already.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `audit.action.*` in en and vi covers every action any service records.
- **FR-002**: A Vitest test reads the server's C# source, collects every action passed to `IAuditTrail.RecordAsync` (literals, ternaries, and declared indirect calls), and fails on any without a label in either language or any indirect call not declared.
- **FR-003**: `/status` lists the eight services with health routes, by name, and drops the orchestrator note.
- **FR-004**: A Vitest test holds `HEALTH_SERVICES` to the gateway's health routes.

## Success Criteria *(mandatory)*

- **SC-001**: No audit action shows as a raw key (tested against the server source).
- **SC-002**: The status page lists all eight services (tested against the gateway configuration).

## Assumptions

- The client's tests run with the whole repository checked out (true locally and in CI's client job), so they can read `server/`.
