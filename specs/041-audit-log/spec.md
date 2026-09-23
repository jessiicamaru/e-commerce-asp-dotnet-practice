# Feature Specification: An audit log of who did what

**Feature branch**: `041-audit-log` | **Issue**: #86 | **Created**: 2026-09-24 | **Status**: Draft

## What is wrong

Nothing records who did what. An administrator records a payout, cancels an order or deletes a product,
and the only trace is the row it changed: no actor, no "what was it before". With staff roles about to be
granted to people other than the one administrator (#88), "who changed this" must have an answer.

## Decided with the user (2026-09-24)

- One new service, **Activity**, holds the audit log (and, in #87, in-app notifications).

## User Scenarios

### US1 - An administrator reads what happened (P1)

1. The admin console has an **Audit log**: newest first, filtered by category, actor, subject and period,
   a page at a time.
2. Opening an entry shows who, when, what, on which thing, from which service - and **what changed**,
   field by field, old → new.

### US2 - Everything that matters is recorded, once (P1)

1. Every action in the list below produces exactly one entry, in the right category, with the actor.
2. A change that fails leaves no entry; a redelivered message leaves one.
3. Secrets never reach the log: passwords, tokens and hashes are redacted wherever they appear.

**Categories and what they hold**

| Category | Examples |
| :-- | :-- |
| System | a sweeper that changed something (reservations expired, deliveries auto-confirmed, images reclaimed) |
| Security | signed in, sign-in refused, shop registered by a new account |
| User | account registered, shop renamed, addresses changed |
| Catalog | product listed, edited, withdrawn; prices set; variant added; stock set; categories |
| Order | order placed, cancelled, parcel prepared/shipped/received |
| Payment | payment charged or refused, refund recorded, payout recorded |
| Moderation | (from #88-#91) roles, locks, approvals, takedowns |

## Requirements

- **FR-001** An entry is written in the same transaction as the change it describes.
- **FR-002** An entry is recorded at most once, whatever the delivery count.
- **FR-003** Updates carry a before and after snapshot; the diff lists changed fields only.
- **FR-004** Only an administrator reads the log.
- **FR-005** No secret is ever stored.

## Out of scope

- Retention and archiving; exporting.

## Success Criteria

- **SC-001** Each listed action yields exactly one entry with the correct category, actor and diff.
- **SC-002** `verify-saga.sh` passes; the new service's tests pass in CI.
