# Research: An audit log of who did what

> Completed on 2026-09-27, after the feature merged (#93), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-24

D1-D6 were written with the plan; D7-D10 record decisions visible in the merged code and the pull request
that the first draft did not write down. Where the first draft gave no rejected alternative, the
reconstruction says so rather than inventing one.

---

## D1 - One new service, Activity

**Decision**: a new service, **Activity**, with its own Clean Architecture layers, its own database
(5440 / `ecommerce_activity_db`), REST on 5063.

**Rationale**: Audit entries and (#87) notifications are both **sinks of business events**: every service
says what happened, and Activity keeps it for someone to read - staff for the audit, the user for
notifications. One service is one database, one gateway cluster, one image. The user chose one service
for both (2026-09-24).

**Alternatives considered**:

- **A table in each service.** Rejected: no single place to read, and reading "who did what" across
  services would mean cross-service queries, which Principle I forbids.
- **Two services, one for the audit and one for notifications.** Rejected by the user: twice the
  infrastructure for two sinks of the same shape.

---

## D2 - Written by the service that changed, through its outbox

**Decision**: A contract `AuditEntryRecorded` is published by the service that made the change, **before
its single `SaveChangesAsync`** - so the row and the outbox message commit together (Principle III). An
entry for a change that rolled back, or a change with no entry, cannot happen. Activity consumes it.

**Rationale**: only the service making the change knows the actor, the before-state and whether the
change committed. The outbox is the one mechanism in the repository that ties a message to a
transaction.

**Alternatives considered**:

- **Activity inferring the log from domain events that already exist.** Rejected: most actions have none,
  and none carries the actor or a before-snapshot.
- **A synchronous call to Activity** (not in the first draft; the reason the outbox design visibly
  avoids it): an audit outage would refuse orders, and the call could succeed for a change that then
  rolled back.

---

## D3 - A shared `IAuditTrail`

**Decision**: `Ecommerce.Shared/Audit`: `IAuditTrail.RecordAsync(category, action, subjectType, subjectId,
summary, before, after)` fills the actor from `ICurrentUser` (null → the system), the service name, the
time and a fresh entry id, serialises snapshots with **redaction** of any property whose name contains
`password`, `token`, `secret` or `hash`, and publishes. `Ecommerce.Shared` gains a reference to
`Ecommerce.Contracts`, which is pure records.

**Rationale**: five services record, and each must fill the same fields the same way. One implementation
means one place where redaction happens and one role ranking. Each service registers it with
`AddAuditTrail("<service>")`.

**Alternatives considered** (none recorded in the first draft; this is the alternative the code avoids):

- **A copy per service.** Rejected: CLAUDE.md's warning about per-service duplicates of shared building
  blocks applies - two copies drift, and a copy that forgets redaction leaks a secret.

---

## D4 - Idempotent by the entry id

**Decision**: The entry id is minted by the publisher (`Guid.CreateVersion7()`) and is the primary key;
the consumer inserts with `ON CONFLICT DO NOTHING`. A redelivery is a no-op.

**Rationale**: Principle III requires idempotency by a database constraint, not configuration alone. The
consumer endpoint also runs MassTransit's EF inbox (`UseEntityFrameworkOutbox<ActivityDbContext>`), but
the primary key is what holds if that is ever misconfigured. The mutation that removed the `ON CONFLICT`
turned a test red.

**Alternatives considered** (none recorded in the first draft; these follow the repository's precedent):

- **Inbox only.** Rejected for the same reason as specs/001 D3: the whole guarantee would sit in
  configuration no test catches.
- **An id minted by Activity.** Rejected: a redelivery would then be a new row.

---

## D5 - The diff is computed once, when recorded

**Decision**: Activity flattens both JSON snapshots to paths (`name`, `prices.VND`, `options[0].value`)
and stores the changed paths with old and new values. The reader never recomputes; the page shows the
stored list.

**Rationale**: the diff of a stored entry never changes, so computing it per read is waste and a second
place to get it wrong. Paths are sorted, so the same two snapshots always give the same list; the list is
cut at 200 changes (`AuditDiff.MaxChanges`). A creation lists every field as added, a deletion every field
as removed. A JSON `null` leaf is compared as the text `null` - the test that found the crash is
`AuditDiffTests`.

**Alternatives considered** (none recorded in the first draft; the ones the stored shape rules out):

- **Store only the diff.** Rejected: the snapshots are what an administrator reads when the diff alone
  does not explain a change.
- **Compute the diff in the recording service.** Rejected: five copies of the flattening, and a larger
  message.

---

## D6 - Admin-only reads

**Decision**: `GET /api/audit` (filters + paging), `GET /api/audit/{id}`, `GET /api/audit/summary` (count
per category for the tabs). `[Authorize(Roles = "Admin")]`.

**Rationale**: the log names who did what to whom. The controller's comment gives the reason for
excluding moderators: a moderator reading who locked them would be reading their own file.

**Alternatives considered** (not recorded in the first draft beyond the controller's comment):

- **Staff (Admin and Moderator).** Rejected for the whole log; a moderator's own decisions are served
  separately later (specs/045, `GET /api/audit/mine`).

---

## D7 - A `stage` callback for guarded statements in their own transaction

**Decision**: repository methods that open their own transaction and run a guarded `UPDATE`
(`TryMoveShipmentAsync`, `TryConfirmDeliveryAsync`, `AutoConfirmDeliveriesAsync`, cancellation,
`TryRecordAsync` for payouts) take an optional `stage` callback. The handler records the entry inside it,
and the repository saves it in that transaction - only when the guard matched.

**Rationale**: those writes are not "stage then one `SaveChangesAsync`" - the guard decides inside the
transaction whether anything happens. Recording before the call would record a move that then affected
zero rows; recording after would be outside the transaction. The mutation that saved a parcel move without
its entry turned a test red.

**Alternatives considered**: not recorded in the first draft. Recording after the repository returns was
the obvious alternative and is the Principle III defect this avoids.

---

## D8 - Redaction by property name, before the snapshot leaves the service

**Decision**: `AuditSnapshot.Serialize` walks the JSON tree and replaces with `***` the value of any
property, at any depth, whose name contains `password`, `token`, `secret` or `hash`, case-insensitively.

**Rationale**: a snapshot is often a whole entity, and a secret a handler forgot to strip would otherwise
be stored forever in a table administrators read. Redacting in the publisher means the secret never
reaches the broker or Activity. Name-based matching errs toward over-redaction, which costs a hidden
harmless field at worst.

**Alternatives considered**: not recorded. An allow-list of fields per entity was the obvious alternative;
it would need maintaining at every call site.

---

## D9 - The actor: the caller's most powerful role, or an explicit actor, or the system

**Decision**: with a signed-in caller, the actor is their id, email and the first of `Admin`, `Moderator`,
`Seller`, `Customer` they hold. Sign-in and registration pass an explicit `AuditActor` (built by
Identity's `AuditActors.Of(user)`), because nobody is signed in yet. A refused sign-in for an address with
no account records an empty actor. With no caller (a consumer, a sweeper) the entry is the system's.

**Rationale**: an entry labelled "Customer" for something an administrator did would mislead; the most
powerful role is the one that explains why the action was allowed. On the anonymous sign-in endpoint
"whoever is calling" means nothing.

**Alternatives considered**: not recorded.

---

## D10 - Product images are saved just after their change

**Decision**: the image switch keeps its own guarded `UPDATE` (specs/019), and its entry is saved
immediately after rather than inside it.

**Rationale**: recorded in the pull request and CLAUDE.md as the one exception. Rewriting the image
switch to take a `stage` was not done in this feature.

**Alternatives considered**: a `stage` callback like D7 - not taken here; the reason is not recorded.
