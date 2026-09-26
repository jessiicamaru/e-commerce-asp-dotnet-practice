# Phase 1 Data Model: Limits on the sign-in and email endpoints

> Written on 2026-09-27, after the feature merged (#145), from the code at that merge, the pull request and
> [docs/features/auth/db-design.md](../../docs/features/auth/db-design.md).

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One new table in `ecommerce_identity_db`, added by migration `20260925044212_AddSignInThrottles`. The gateway's
allowances are in memory and have no schema. The reset interval reads the existing `password_reset_tokens`
(specs/061) and locks the existing `users` row.

---

## `sign_in_throttles`

Mapped by `SignInThrottleConfiguration` from `SignInThrottle` (Identity Domain). Written only by raw SQL in
`SignInThrottleRepository`.

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `EmailKey` | varchar(255) | **PK** | `EmailKey.For(email)` - trimmed, lower-cased; the same key sign-in looks accounts up by. Present for an address with no account too |
| `Failures` | integer | Not null | Wrong passwords in the current window; back to 0 when a pause starts |
| `WindowStartedAt` | timestamptz | Not null | When the current window of 15 minutes began |
| `BlockedUntil` | timestamptz | Nullable | Sign-in for this email is paused until then |

No other index: every statement is by primary key, except the hourly purge, which scans a table that only holds
recent failures.

## Statements

| Operation | SQL shape | When |
| :--- | :--- | :--- |
| Is it paused? | `SELECT "BlockedUntil" ... WHERE "EmailKey" = @k AND "BlockedUntil" > now` | First thing in every sign-in |
| Count a failure | one `INSERT ... ON CONFLICT ("EmailKey") DO UPDATE SET ... RETURNING "Failures", "BlockedUntil"` | Wrong password or unknown email |
| Clear | `DELETE ... WHERE "EmailKey" = @k` | Right password; a successful reset |
| Purge | `DELETE ... WHERE ("BlockedUntil" IS NULL OR "BlockedUntil" <= now) AND "WindowStartedAt" <= now - 15 min` | `SignInThrottleSweeper`, every `SignIn:PurgeMinutes` (60) |

## A row's life

```text
  (no row) ──wrong──▶ Failures 1..4, window open ──5th wrong──▶ Failures 0, BlockedUntil = +5 min
      ▲                   │                                           │
      │                   ├── right password / reset ──▶ deleted      ├── (sign-ins get 429 until then)
      │                   └── window over ──▶ next wrong restarts at 1 └── pause over ──▶ next wrong counts from 1
      └────────────── purged hourly once no pause is running and the window is over
```

## Settings

| Setting | Default | Validated |
| :--- | :--- | :--- |
| `SignIn:MaxFailures` | 5 | at least 1, at start |
| `SignIn:WindowMinutes` | 15 | at least 1, at start |
| `SignIn:CooldownMinutes` | 5 | at least 1, at start |
| `SignIn:PurgeMinutes` | 60 | read with a default |
| `RateLimits:<sign-in|email|session>:PermitLimit` / `WindowSeconds` (gateway) | 30/60, 5/60, 60/60 | at least 1, at start |

## Rollback

A new table only. An earlier image ignores it and simply does not pause anybody.
