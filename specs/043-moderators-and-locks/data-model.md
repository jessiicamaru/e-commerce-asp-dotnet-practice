# Phase 1 Data Model: Moderators, locks and bans

> Written on 2026-09-27, after the feature merged (#95), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One migration in Identity's `ecommerce_identity_db`, adding four nullable columns to `users`. One new row in
`roles`, written at startup rather than by the migration. No new table, no index, no constraint. The audit
entries and notifications this feature publishes land in Activity's database through messages that already
existed (see [contracts/messages.md](./contracts/messages.md)).

---

## `users` - four columns added

Migration `20260923202324_AddAccountLocks`
(`server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Migrations/`). Lengths come from
`UserConfiguration` (`HasMaxLength(500)` on both reasons).

| Column | Type | Null | Meaning |
| :-- | :-- | :-- | :-- |
| `LockedUntil` | `timestamp with time zone` | yes | Signed out and refused sign-in until then. A value in the past means not locked |
| `LockReason` | `character varying(500)` | yes | Why, as staff wrote it; shown to the person at sign-in |
| `BannedAt` | `timestamp with time zone` | yes | When the ban began. Not null means banned, until an administrator lifts it |
| `BanReason` | `character varying(500)` | yes | Why, as the administrator wrote it; shown to the person at sign-in |

`Down` drops the four columns. Nothing is backfilled: every existing account starts with all four null,
which reads as neither locked nor banned.

**Derived, not stored** (`User`):

- `IsLocked(now)` = `LockedUntil is { } until && until > now`. A lock that has run out needs no clean-up
  job, and `UserAdminResponse.From` shows it as no lock (both fields null).
- `IsBanned` = `BannedAt is not null`.

**Validation that stands in for constraints**: the database has no check on these columns. The rules are
in the Application layer: `LockUserCommandValidator` (days 1 to 365, reason not blank and at most 500),
`BanUserCommandValidator` (reason not blank and at most 500), and the handler's 30-day cap for a caller who
is not an administrator.

---

## `roles` - one row added at startup

`RoleNames.Descriptions` gains `Moderator` = "Reviews what others publish and may lock an account for up to
30 days.". `DataInitializer.SeedRolesAsync` inserts every name in `Descriptions` that is not yet in `roles`,
with a `Guid.CreateVersion7()` id, so the row appears the first time the new image starts against an
existing database. Ids therefore differ per environment; nothing refers to a role by id.

| Column | Value for the new row |
| :-- | :-- |
| `Id` | generated at startup (v7) |
| `Name` | `Moderator` |
| `Description` | as above (`character varying(255)`) |

## `user_roles` - rows written by grant and revoke

The existing many-to-many join (`user_id`, `role_id`). A grant adds the `(user, Moderator)` row through the
tracked `User.Roles` collection; a revoke removes it. Neither is written when it would change nothing.

## `refresh_tokens` - `RevokedAt` set on a stop

No schema change. A lock or a ban calls the existing `RevokeAllRefreshTokensAsync`, which sets `RevokedAt =
now` on every row of the user where it is null, in one `ExecuteUpdate`, after the account is saved.

---

## Account states

There is no status column. The state is read from the two pairs, and both stops can hold at once.

```text
                      lock (days, reason)                 ban (reason) - administrators only
        ┌────────┐ ──────────────────────▶ ┌─────────┐    ┌────────┐ ─────────────────▶ ┌────────┐
        │ Active │                          │ Locked  │    │  any   │                    │ Banned │
        └────────┘ ◀────────────────────── └─────────┘    └────────┘ ◀───────────────── └────────┘
                  unlock, or LockedUntil passes              lift (unban) - administrators only
```

| Transition | Columns written | Also | Who |
| :-- | :-- | :-- | :-- |
| lock | `LockedUntil = now + days`, `LockReason`, `UpdatedAt` | audit `AccountLocked`; every refresh token revoked | Staff, subject to `EnsureMayStop` and the 30-day cap |
| unlock | `LockedUntil = null`, `LockReason = null` | audit `AccountUnlocked` - only if a lock was set | Staff (no target rules at this merge; specs/050 added them) |
| lock runs out | nothing | nothing | the clock |
| ban | `BannedAt = now`, `BanReason`, `UpdatedAt` | audit `AccountBanned`; every refresh token revoked | Admin, subject to `EnsureMayStop` |
| lift | `BannedAt = null`, `BanReason = null` | audit `BanLifted` - only if a ban was set | Admin |
| grant Moderator | a `user_roles` row | audit `RoleGranted`; notice `ModeratorGranted` - only if not held; 409 if banned | Admin |
| revoke Moderator | the `user_roles` row removed | audit `RoleRevoked`; notice `ModeratorRevoked` - only if held | Admin |

Unlock and lift write nothing when there is nothing to clear, and grant and revoke write nothing when the
role is already as asked; that is what makes a repeat a no-op (Principle III). A lock or ban on an account
already locked or banned **replaces** the date or the reason - there is no guard on the current state.

**What each state refuses**:

| State | Sign-in, wrong password | Sign-in, right password | Refresh |
| :-- | :-- | :-- | :-- |
| Active | 401 | 200 | 200 |
| Locked | 401 (same message as any account) | 403 "This account is locked until … UTC: {reason}" | 401 |
| Banned (with or without a lock) | 401 | 403 "This account is banned: {reason}" | 401 |

---

## What the audit diff compares

Every write snapshots, before and after, an anonymous object of `Roles` (names, sorted), `LockedUntil`,
`LockReason`, `BannedAt` and `BanReason` - "the roles and the two stops, nothing personal". No email, name
or password hash enters the diff.

---

## What did not change, and why

- **No status enum** (research D1): an added enum value would stop an earlier image parsing the row.
- **`IsActive`** is untouched and still unread.
- **No index** for search (research D11): not measured, accepted at this size.
- **No change to `refresh_tokens`** or `roles`' schema.
- **The migration only adds nullable columns**, so an Identity image from before #95 still reads and writes
  `users`: it simply never sets or reads the four columns. A person locked while such an image serves
  sign-in would not be refused by it - the ordinary cost of rolling back a feature, not a schema break.
