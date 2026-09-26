# Phase 1 Data Model: Audit gaps and the misleading reuse warning

> Written on 2026-09-27, after the feature merged (#141), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md).

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration was added or changed.** The feature writes more rows into tables that
already existed and changes how one existing pair of columns is read. An older image therefore runs against the
same schema unchanged.

---

## Rows written

| Table (service) | What is written | When |
| :--- | :--- | :--- |
| MassTransit outbox tables (Catalog, Identity) | One `AuditEntryRecorded` message per recorded write | In the same `SaveChangesAsync` as the write |
| `audit_entries` (Activity, 5440) | One row per entry, `INSERT ... ON CONFLICT DO NOTHING` on the publisher's entry id | When Activity consumes the message (unchanged since specs/041) |

The new actions, as stored in `audit_entries."Action"` (varchar(64)):

| Action | Category | SubjectType | Before | After |
| :--- | :--- | :--- | :--- | :--- |
| `CategoryTranslated` | Catalog | Category | `{ Name, Description }` of the existing translation, or null | `{ Name, Description }` as set (trimmed) |
| `CategoryTranslationRemoved` | Catalog | Category | `{ Language, Name, Description }` | null |
| `ProductTranslationRemoved` | Catalog | Product | `{ Language, Name, Description }` | null |
| `DefaultAddressChanged` | User | Address | null | null |
| `SignedOut` | Security | User | null | null |
| `SessionReuseDetected` | Security | User | null | null |

(Six names: `SessionReuseDetected` is the reuse event of US2; the other five are the writes of US1.)

## Columns read differently

`refresh_tokens` in `ecommerce_identity_db` (unchanged schema):

| Column | Role in the new rule |
| :--- | :--- |
| `RevokedAt` (timestamptz, null while active) | Set by rotation, sign-out elsewhere, a lock, a ban and a reuse sweep |
| `ReplacedByToken` (text, null unless rotated) | Set **only** by rotation. Non-null plus `now - RevokedAt > 10 s` is reuse; null is a stale tab |

## Decision table for a presented refresh token

| `RevokedAt` | `ReplacedByToken` | Age of revocation | Outcome |
| :--- | :--- | :--- | :--- |
| null | - | - | Rotated normally (unchanged) |
| set | set | <= 10 s | 401; nothing else (concurrent refresh from two tabs) |
| set | set | > 10 s | 401; `SessionReuseDetected` saved; every active token of the user revoked; warning logged |
| set | null | any | 401; Information logged; nothing else revoked (**new** - was treated as reuse) |
