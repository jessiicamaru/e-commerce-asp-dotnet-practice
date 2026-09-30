# Feature Specification: Old notices and audit entries are removed on a schedule

**Feature Branch**: `116-activity-retention` | **Created**: 2026-10-01 | **Issue**: #221

**Status**: Draft

**Input**: Issue #221 - "the audit log and notifications grow without bound": "A retention setting per table (read
notifications after N days; audit entries after N years or never), and a sweeper shaped like the others, safe on several
instances. The audit log: decide whether it may be deleted at all, or only archived."

## Why

Activity keeps every notice anybody was sent and every audit entry any service recorded, for ever. A notice read months
ago helps nobody and slows every bell; the audit log is the shop's security record, and keeping it for ever may be
right - but that should be a decision, written down, not the absence of one.

## User Scenarios & Testing *(mandatory)*

### US1 - Read notices go after a while (Priority: P1)

**Acceptance Scenarios**:

1. **Given** a notice read more than 90 days ago (the default), **When** the sweeper runs, **Then** it is deleted.
2. **Given** a notice read less than 90 days ago, **Then** it stays.
3. **Given** an unread notice of any age, **Then** it stays - somebody has not seen it yet.
4. **Given** thousands to delete, **Then** they go in batches, each its own short statement, and the sweep carries on
   until none are left.
5. **Given** two Activity instances sweeping at once, **Then** every row is deleted once and nothing fails.

---

### US2 - The audit log is kept unless an operator says otherwise (Priority: P1)

**Decision** (research D2): the audit log is kept for ever by default. An operator may set a retention in whole years
(at least 1); entries older than that are then deleted - and the trim is itself recorded in the audit log, so the log
says it was shortened, when, and by how much. There is no archive.

**Acceptance Scenarios**:

1. **Given** no audit retention set, **Then** no audit entry is ever deleted.
2. **Given** a retention of 5 years, **Then** entries older than 5 years are deleted and a `System` / `AuditTrimmed`
   entry records the cutoff and the count - one per batch, in that batch's transaction; newer entries stay.
3. **Given** a retention set below 1 year, or read notices kept less than 1 day, **Then** Activity refuses to start and
   says which setting is wrong.

### Edge Cases

- **A notice read and then the setting shortened**: the next sweep removes whatever is now past the line - retention
  is applied to what exists, not to what arrives.
- **The sweep's own audit entry**: recorded only when the trim deleted something, so an idle sweeper adds nothing.
- **A deleted account's notices** (specs/112) are already gone; this changes nothing for them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `Retention:ReadNotificationDays` (default 90, at least 1): read notices whose `ReadAt` is older are deleted.
- **FR-002**: `Retention:AuditYears` (default none - keep for ever; when set, at least 1): audit entries whose
  `OccurredAt` is older are deleted, each batch with an `AuditTrimmed` entry recording the cutoff and its count, in the
  same transaction.
- **FR-003**: A sweeper in Activity, every `Retention:IntervalMinutes` (60), deletes in batches of `Retention:BatchSize`
  (1000) until a batch comes back short; safe on several instances.
- **FR-004**: Activity refuses to start with a setting out of range, naming it.

## Success Criteria *(mandatory)*

- **SC-001**: Read notices older than the setting are removed; unread ones never are (tested).
- **SC-002**: With no audit retention, no audit entry is ever removed (tested).
- **SC-003**: Two sweeps at once remove each row once without error (tested).

## Assumptions

- Retention is by time only; no per-category audit retention.
- Deleting from the audit log needs no approval beyond the operator's setting, which is itself configuration under
  change control.
