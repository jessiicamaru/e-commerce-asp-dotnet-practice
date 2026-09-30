# Feature Specification: Administrators are told when an email fails for good

**Feature Branch**: `115-email-failure-alert` | **Created**: 2026-10-01 | **Issue**: #222

**Status**: Draft

**Input**: Issue #222 - "nobody is told when an email fails for good": "Administrators get an in-app notice when an email
fails for good (one per email, or a digest). The overview page shows how many are failed."

## Why

An email the mail server refuses is retried 12 times over several hours (specs/060) and then marked `Failed`. After that
it waits for an administrator to happen to open `/admin/email-delivery` (specs/087). A person who never received their
order confirmation, or the link to reset their password, is not told either - so nobody is.

## User Scenarios & Testing *(mandatory)*

### US1 - An administrator is told (Priority: P1)

**Acceptance Scenarios**:

1. **Given** an email that exhausts its attempts, **When** the dispatcher marks it `Failed`, **Then** each administrator
   gets one in-app notice saying how many emails could not be delivered, linking to the failed ones.
2. **Given** a mail server down for hours - a hundred emails failing across many sweeps - **Then** administrators get at
   most one notice an hour, each counting the failures since the last one; none is lost and none is counted twice.
3. **Given** an email refused for good at once (no such recipient, no words for its template), **Then** it counts the
   same way.
4. **Given** a failed email an administrator retries (specs/087) that fails again, **Then** it is counted again.
5. **Given** several Identity instances sweeping at once, **Then** each failure is counted in exactly one notice.

---

### US2 - The Overview says how many are failed (Priority: P2)

**Acceptance Scenarios**:

1. **Given** failed emails, **When** an administrator opens `/admin/overview`, **Then** it shows how many, linking to
   the failed list; with none, it says so.

### Edge Cases

- **Failures from before this feature**: they are counted in the first notice after it is deployed - an administrator
  learns of them once, which is what the feature is for.
- **No administrator** (never: one is seeded) - the failures are marked counted and nobody is told; the Overview still
  shows them.
- **An administrator who deleted their account** cannot exist (staff cannot delete, specs/112); a banned one is still
  told - the notice is harmless and the role is what decides.
- **The notice's own delivery** is in-app (Activity), never an email - an alert about failing email that is itself an
  email would fail with the rest.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `outgoing_emails.FailureAlertedAt` marks a failed email as counted in a notice; a retry clears it.
- **FR-002**: After each dispatch sweep, in its transaction, one guarded statement claims every failed, uncounted email -
  only when no notice went out in the last hour - and each administrator is sent `EmailsFailed` with the count and a
  link to `/admin/email-delivery`.
- **FR-003**: The kind `EmailsFailed` carries `failed` (the count), declared in `notification-kinds.json`, worded in
  both languages, and rewordable (specs/078) through a `{{failed}}` placeholder.
- **FR-004**: The Overview shows the number of failed emails from the email log (`GET /api/emails?status=Failed`).

## Success Criteria *(mandatory)*

- **SC-001**: An email that exhausts its attempts produces one notice to each administrator (tested).
- **SC-002**: Failures within the hour after a notice are carried to the next one, never lost or doubled (tested,
  including two sweeps at once).

## Assumptions

- A digest, not a notice per email: a mail server down for an afternoon would otherwise bury the bell.
- An hour between notices matches the dispatcher's longest retry interval (specs/060).
