# Data Model: An audit log of who did what

> Written on 2026-09-27, after the feature merged (#93), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

One table in the new database `ecommerce_activity_db` (host port 5440), plus MassTransit's `InboxState`,
`OutboxState` and `OutboxMessage` from `AddTransactionalOutboxEntities()`. All are created by one
migration, `20260923192252_InitialCreate` in `Ecommerce.Activity.Infrastructure/Migrations/`.

No table in any other service changed. The five instrumented services write only their existing
`OutboxMessage` rows - an audit entry travels as one more outbox message in the transaction that made the
change.

---

## `audit_entries`

One row per thing that happened. Written once, never changed: "an audit log that can be edited is not
one" (`AuditEntry`'s own comment). Mapped by `AuditEntryConfiguration`.

| Column | Type | Constraints | Meaning |
| :-- | :-- | :-- | :-- |
| `Id` | `uuid` | PK, `ValueGeneratedNever()` | Minted by the publisher (`Guid.CreateVersion7()`), so a redelivered message is the same entry (research D4) |
| `Category` | `character varying(32)` | not null | One of `System`, `Security`, `User`, `Catalog`, `Order`, `Payment`, `Moderation` (`AuditCategory.All`) |
| `Action` | `character varying(64)` | not null | PascalCase: `OrderPlaced`, `PriceSet`, `SignInRefused` |
| `ActorId` | `uuid` | null | Who did it; null means the system (a sweeper, a message, nobody signed in) |
| `ActorEmail` | `character varying(256)` | null | Copied at the time; the `actor` filter matches part of it |
| `ActorRole` | `character varying(32)` | null | The most powerful role held: `Admin`, `Moderator`, `Seller`, `Customer` |
| `SubjectType` | `character varying(64)` | not null | At the merge: `Order`, `Product`, `Variant`, `User`, `Seller`, `Address`, `Reservation`, `Parcel`, `Category`, `ImageStore` |
| `SubjectId` | `character varying(128)` | null | Text, not uuid, so any subject's key fits; null for a batch (a sweep) |
| `Summary` | `character varying(1000)` | not null | One line a person reads in the list |
| `Before` | `jsonb` | null | Snapshot before, secrets already redacted by the publisher; null for a creation |
| `After` | `jsonb` | null | Snapshot after, redacted; null for a deletion |
| `Changes` | `jsonb` | not null | Array of `{ path, before, after }`, computed once when recorded (research D5) |
| `ChangeCount` | `integer` | not null | Length of `Changes`, so the list shows it without reading the `jsonb` |
| `Service` | `character varying(32)` | not null | `identity`, `catalog`, `inventory`, `order`, `payment` - the name given to `AddAuditTrail` |
| `OccurredAt` | `timestamp with time zone` | not null | When the publisher recorded it |
| `RecordedAt` | `timestamp with time zone` | not null | When Activity kept it |

**Indexes** - the log is read newest first, by category, by actor and by subject:

| Name | Columns |
| :-- | :-- |
| `IX_audit_entries_OccurredAt` | `OccurredAt` |
| `IX_audit_entries_Category_OccurredAt` | `Category`, `OccurredAt` |
| `IX_audit_entries_ActorId_OccurredAt` | `ActorId`, `OccurredAt` |
| `IX_audit_entries_SubjectType_SubjectId` | `SubjectType`, `SubjectId` |

No check constraint restricts `Category`: the set is enforced where entries are written (`AuditCategory`)
and where they are queried (the list's validator refuses an unknown category).

## How a row is written

One statement, in `AuditRepository.TryAddAsync`:

```sql
INSERT INTO audit_entries ("Id", "Category", "Action", ..., "OccurredAt", "RecordedAt")
VALUES (..., CAST(@before AS jsonb), CAST(@after AS jsonb), CAST(@changes AS jsonb), ...)
ON CONFLICT ("Id") DO NOTHING
```

It returns whether this call inserted the row; a redelivery affects zero rows. There are no state
transitions: an entry has no states.

## The diff's shape

`Changes` holds a list of `AuditChange(Path, Before, After)`, serialised with web defaults:

```json
[
  { "path": "price", "before": 1200000, "after": 1150000 },
  { "path": "options[0].value", "before": "Đen", "after": "Bạc" }
]
```

`before` and `after` are JSON values, not display strings, and JSON `null` when the path is absent on
that side. Paths are sorted ordinally; at most 200 entries (`AuditDiff.MaxChanges`). An empty object or
array is a leaf value in its own right.

## What did not change, and why it matters

- **No column in another service.** Principle I: Activity never reads another service's database, and no
  service reads Activity's.
- **Addresses** are recorded as "an address changed" with no content in the snapshots.
- The table is created, not altered, so there is no older image of Activity to strand (expand-then-
  contract does not arise).
