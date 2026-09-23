# Research: An audit log of who did what

## D1 - One new service, Activity

Audit entries and (#87) notifications are both **sinks of business events**: every service says what
happened, and Activity keeps it for someone to read - staff for the audit, the user for notifications.
One service is one database, one gateway cluster, one image. Its own Clean Architecture layers, its own
database (5440 / `ecommerce_activity_db`), REST on 5063.

## D2 - Written by the service that changed, through its outbox

A contract `AuditEntryRecorded` is published by the service that made the change, **before its single
`SaveChangesAsync`** - so the row and the outbox message commit together (Principle III). An entry for a
change that rolled back, or a change with no entry, cannot happen. Activity consumes it.

Rejected: Activity inferring the log from domain events that already exist. Most actions have none, and
none carries the actor or a before-snapshot.

## D3 - A shared `IAuditTrail`

`Ecommerce.Shared/Audit`: `IAuditTrail.RecordAsync(category, action, subjectType, subjectId, summary,
before, after)` fills the actor from `ICurrentUser` (null → the system), the service name, the time and a
fresh entry id, serialises snapshots with **redaction** of any property whose name contains `password`,
`token`, `secret` or `hash`, and publishes. `Ecommerce.Shared` gains a reference to
`Ecommerce.Contracts`, which is pure records.

## D4 - Idempotent by the entry id

The entry id is minted by the publisher and is the primary key; the consumer inserts with
`ON CONFLICT DO NOTHING`. A redelivery is a no-op.

## D5 - The diff is computed once, when recorded

Activity flattens both JSON snapshots to paths (`name`, `prices.VND`, `options[0].value`) and stores the
changed paths with old and new values. The reader never recomputes; the page shows the stored list.

## D6 - Admin-only reads

`GET /api/audit` (filters + paging), `GET /api/audit/{id}`, `GET /api/audit/summary` (count per category
for the tabs). `[Authorize(Roles = "Admin")]`.
