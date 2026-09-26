# Data Model: Sign-in refusal

> Written on 2026-09-27, after the feature merged (#130), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**No table, column, index or migration changed.** The feature changes the shape of an HTTP answer only.

## Where the facts are read from

The refusal is built in `LoginCommandHandler` from columns specs/043 added to Identity's `users` table
(`ecommerce_identity_db`, migration `20260923202324_AddAccountLocks`):

| Column | Type | Becomes |
| :--- | :--- | :--- |
| `LockedUntil` | `timestamp with time zone`, null | `until` - `DateTime.SpecifyKind(LockedUntil, Utc)`, sent only for a lock |
| `LockReason` | `character varying(500)`, null | `reason` for a lock |
| `BannedAt` | `timestamp with time zone`, null | decides `IsBanned`; not sent |
| `BanReason` | `character varying(500)`, null | `reason` for a ban |

`User.IsLocked(now)` is `LockedUntil > now`, so an expired lock is no lock and no refusal. A ban is checked
first: a person both banned and locked is refused as banned.

## The in-memory shape

`ForbiddenException.Facts` is an `IReadOnlyDictionary<string, object?>`, empty when none are given. For
sign-in:

| Key | Value type | Present |
| :--- | :--- | :--- |
| `code` | `string` - `AccountLocked` or `AccountBanned` | always |
| `until` | `DateTime` (kind UTC) | locks only |
| `reason` | `string` | always - the lock and ban validators require a non-blank reason of at most 500 characters |

## Compatibility

Nothing stored changed, so an earlier Identity image reads every row as before; an earlier image simply
sends the 403 without the extensions, and the storefront then shows `detail` (FR-003).
