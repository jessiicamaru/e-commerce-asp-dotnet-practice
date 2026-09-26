# Data Model: Unlock rules

> Written on 2026-09-27, after the feature merged (#131), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**No table, column, index or migration changed** (FR-002). The rule reads what specs/043 already stores.

## What the rule reads

Identity's `users` table (`ecommerce_identity_db`, migration `20260923202324_AddAccountLocks`):

| Column | Type | Used for |
| :--- | :--- | :--- |
| `Id` | `uuid` | "Is the target the caller?" |
| `LockedUntil` | `timestamp with time zone`, null | Time still to run: `LockedUntil - now` |

and the target's roles (`user_roles` / `roles`), for "is the target a moderator?".

## What an unlock writes (unchanged)

An allowed unlock of a locked account sets `LockedUntil = null`, `LockReason = null`, `UpdatedAt = now`, and
records an `AccountUnlocked` audit entry (category Moderation) through the outbox before the one save. An
unlock of an account that is not locked writes nothing. A refused unlock writes nothing either - the rule
throws before any change is staged.

## State

```text
  Locked (LockedUntil > now) ──unlock, allowed──▶ Not locked (LockedUntil null)
          │
          └── unlock, refused (self / moderator target / > 30 days for a moderator) ──▶ unchanged
```

A ban (`BannedAt`, `BanReason`) is a separate pair and is not touched by unlocking.
