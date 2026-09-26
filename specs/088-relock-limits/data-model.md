# Data Model: Locking again obeys the unlock rule

**No table, column, index or migration changes** (FR-006). The rule reads what specs/043 stores.

## What the rule reads

Identity's `users` table (`ecommerce_identity_db`, 5435; migration `20260923202324_AddAccountLocks`):

| Column | Type | Used for |
| :--- | :--- | :--- |
| `LockedUntil` | `timestamp with time zone`, null | The lock in place: is the new end sooner, and how long is still to run |

and the caller's roles from the token (`ICurrentUser.IsInRole("Admin")`).

## What a lock writes (unchanged when allowed)

`LockedUntil = now + days`, `LockReason = reason`, `UpdatedAt = now`; through the outbox before the one save:
an `AccountLocked` audit entry (Moderation) with before/after snapshots of roles, lock and ban, an `AccountLocked`
notice, an `AccountLocked` email and `AccessTokensRevoked(user, now, "Locked")`; after the save every refresh
token is revoked.

A **refused** lock writes nothing and publishes nothing: `EnsureMayShorten` throws before the snapshot.

## State

```text
  Locked, T to run ──lock(d) by an administrator──────────────────▶ Locked, d to run
         │
         ├──lock(d) by a moderator, now+d ≥ LockedUntil ──────────▶ Locked, d to run   (extends)
         ├──lock(d) by a moderator, sooner, T ≤ 30 days ──────────▶ Locked, d to run   (within reach)
         └──lock(d) by a moderator, sooner, T > 30 days ──────────▶ unchanged, 403     (#180)

  Not locked / lock run out ──lock(d)────────────────────────────▶ Locked, d to run   (unchanged)
```
