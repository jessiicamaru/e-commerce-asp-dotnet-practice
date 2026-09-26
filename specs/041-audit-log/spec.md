# Feature Specification: An audit log of who did what

> Completed on 2026-09-27, after the feature merged (#93), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature Branch**: `041-audit-log`

**Created**: 2026-09-24

**Status**: Merged (#93, 2026-09-23T19:54Z - 2026-09-24 in local time, UTC+7, which is the date this
record's other headings use)

**Issue**: #86

**Input**: "Nothing records who did what. Add an audit log: every service reports what happened, one place
keeps it, and an administrator reads it by category with what changed."

## What is wrong

Nothing records who did what. An administrator records a payout, cancels an order or deletes a product,
and the only trace is the row it changed: no actor, no "what was it before". With staff roles about to be
granted to people other than the one administrator (#88), "who changed this" must have an answer.

## Decided with the user (2026-09-24)

- One new service, **Activity**, holds the audit log (and, in #87, in-app notifications).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An administrator reads what happened (Priority: P1)

1. The admin console has an **Audit log**: newest first, filtered by category, actor, subject and period,
   a page at a time.
2. Opening an entry shows who, when, what, on which thing, from which service - and **what changed**,
   field by field, old → new.

**Why this priority**: the log exists to be read. Recording entries nobody can see answers no question,
and the first question - "who changed this order?" - is asked by an administrator.

**Independent Test**: as an administrator, place and ship an order, then open the Audit log filtered to
that order: the placement, each parcel step and the receipt appear, newest first, each naming its actor;
opening the placement shows its lines as added fields.

**Acceptance Scenarios**:

1. **Given** entries in several categories, **When** an administrator opens the Audit log, **Then** one
   tab per category shows its count for the chosen period and the list is newest first.
2. **Given** an entry for an edited variant, **When** the administrator opens it, **Then** the dialog
   lists only the fields whose value changed, each with its old and new value.
3. **Given** a filtered view (category, actor, period), **When** its address is shared, **Then** the
   recipient sees the same filter - the filters are kept in the URL.
4. **Given** a signed-in customer or an anonymous caller, **When** they ask for the log, **Then** they are
   refused with 403 and 401 respectively.

---

### User Story 2 - Everything that matters is recorded, once (Priority: P1)

1. Every action in the list below produces exactly one entry, in the right category, with the actor.
2. A change that fails leaves no entry; a redelivered message leaves one.
3. Secrets never reach the log: passwords, tokens and hashes are redacted wherever they appear.

**Why this priority**: a log that misses actions, or records a change that never happened, is worse than
none - it is believed. It shares P1 with US1 because neither is useful without the other.

**Independent Test**: perform each listed action once through its real handler against a real database
and assert one entry with the right category, action and actor; perform a refused action and assert
none; deliver the same entry twice and assert one row.

**Acceptance Scenarios**:

1. **Given** a customer placing an order, **When** it is placed, prepared, shipped and received,
   **Then** the log holds `OrderPlaced`, `ParcelPrepared`, `ParcelShipped` and `ParcelReceived` for that
   order, once each.
2. **Given** a refused change (deleting a product that does not exist, a customer confirming a parcel
   that has not shipped), **When** it is refused, **Then** no entry is recorded.
3. **Given** an entry delivered twice by the broker, **When** Activity processes both, **Then** one row
   exists.
4. **Given** a snapshot containing a property named like `password`, `token`, `secret` or `hash` at any
   depth, **When** it is recorded, **Then** the stored value is `***`.
5. **Given** a sweeper (reservations expired, deliveries auto-confirmed), **When** it changes something,
   **Then** the entry is recorded under **System** with no actor.

**Categories and what they hold**

| Category | Examples |
| :-- | :-- |
| System | a sweeper that changed something (reservations expired, deliveries auto-confirmed, images reclaimed) |
| Security | signed in, sign-in refused |
| User | account registered, shop opened by a new account, shop renamed, addresses changed |
| Catalog | product listed, edited, withdrawn; prices set; variant added; stock set; categories |
| Order | order placed, cancelled, parcel prepared/shipped/received; stock returned from a cancelled order |
| Payment | payment charged or refused, refund recorded, payout recorded |
| Moderation | (from #88-#91) roles, locks, approvals, takedowns |

> Correction (2026-09-27): the table as first written put "shop registered by a new account" under
> Security. The code at the merge records it as `ShopOpened` under **User**
> (`RegisterSellerCommand.cs`), and the table now says so. "Stock returned from a cancelled order"
> (`StockReturned`, Inventory) is recorded under **Order** and was added to that row.

### Edge Cases

- **A change that rolls back.** The entry is published through the same outbox as the change, so it
  cannot exist without the change, nor the change without it.
- **A refused sign-in.** The refusal changes no row, so the entry is the only write; it is saved on its
  own before the refusal is thrown. For an address with no account the actor is empty - never "whoever
  is calling", which on an anonymous endpoint means nothing.
- **Nobody is signed in yet.** Sign-in and registration name the account being signed into or created as
  the actor, passed explicitly.
- **A guarded statement in its own transaction** (parcel moves, deliveries, payouts, cancellation). The
  entry is staged inside that transaction and only by the request that won the guard; a request that
  moved nothing records nothing.
- **Product images.** The image switch is its own guarded `UPDATE` (specs/019), so its entry is saved
  just after the change rather than with it - the one exception.
- **A huge change.** The diff is cut at 200 changed fields; a log entry is not a database dump.
- **A JSON `null` leaf.** Compared as the text `null`, not dereferenced - the diff test found a crash here
  before the merge.
- **A value changing type** (`"5"` to `5`) is a change.
- **An address.** Recorded as "an address changed", never copied into a diff.
- **Redelivery.** The entry id is minted by the publisher; a second delivery inserts nothing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** An entry is written in the same transaction as the change it describes.
- **FR-002** An entry is recorded at most once, whatever the delivery count.
- **FR-003** Updates carry a before and after snapshot; the diff lists changed fields only.
- **FR-004** Only an administrator reads the log.
- **FR-005** No secret is ever stored.
- **FR-006** Each entry names its category (one of System, Security, User, Catalog, Order, Payment,
  Moderation), its action, the subject's type and id, a one-line summary, the service that recorded it
  and when it happened.
- **FR-007** The actor is the signed-in caller, labelled with the most powerful role they hold (Admin,
  Moderator, Seller, Customer, in that order); with no caller the entry belongs to the system.
- **FR-008** The log is read a page at a time, newest first, filtered by category, action, actor (part of
  the email), actor id, subject type and id, and period; an unknown category is refused.
- **FR-009** The number of entries per category for a period is available, for the tabs.
- **FR-010** The diff is computed once, when the entry is kept, and never by the reader.

### Key Entities

- **Audit entry**: one thing that happened - category, action, actor (id, email, role, or none), subject
  (type and id), a summary, the before and after snapshots, the list of changed fields with their count,
  the recording service, when it happened and when Activity kept it. Keyed by the id its publisher
  minted.

## Success Criteria *(mandatory)*

- **SC-001** Each listed action yields exactly one entry with the correct category, actor and diff.
- **SC-002** `verify-saga.sh` passes; the new service's tests pass in CI.
- **SC-003** An order the Bruno collection places and staff ship reads back from `/api/audit` as placed,
  prepared, shipped and received, once each; a customer gets 403 and an anonymous caller 401.
- **SC-004** Removing redaction, the `ON CONFLICT`, a parcel move's staged entry or the refused sign-in
  entry each turns a test red.

## Assumptions

- The audit log is read by the one administrator today and by more staff after #88; moderators do not
  read the whole log (they read their own decisions later, specs/045).
- Entries reach Activity through RabbitMQ, so the log is a moment behind the requests that caused them.
- Cart and the Orchestrator record nothing: neither makes a change a person needs to answer for.

## Out of scope

- Retention and archiving; exporting.
- Moderation entries themselves - the category exists; the actions arrive with #88-#91.
