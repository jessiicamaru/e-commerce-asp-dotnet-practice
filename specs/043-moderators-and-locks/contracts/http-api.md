# HTTP Contract: Moderators, locks and bans

> Written on 2026-09-27, after the feature merged (#95), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature**: [spec.md](../spec.md)

Identity on `http://localhost:5056`, reached through the gateway on `http://localhost:5000`. Seven new
endpoints under `/api/users`, and a change in what `POST /api/auth/login` and `POST /api/auth/refresh`
answer for a stopped account.

Errors follow the project's RFC 7807 shape via `GlobalExceptionHandler` - see
[error-handling-and-shared-building-block.md](../../../docs/architecture/error-handling-and-shared-building-block.md).
`ValidationException` is 400 with an `errors` extension; `NotFoundException` 404, `ConflictException` 409
and, new with this feature, `ForbiddenException` **403 "Forbidden" with the message as `detail`, in every
environment**. A 403 that comes from an `[Authorize(Roles = ...)]` attribute is the authorization layer's,
before any handler runs, and carries no handler sentence.

---

## The account as staff see it

Every endpoint below returns this shape, or a page of it.

```json
{
  "id": "0199a1b2-0000-7000-8000-000000000001",
  "email": "someone@example.test",
  "firstName": "Bruno",
  "lastName": "Moderator",
  "roles": ["Customer", "Moderator"],
  "createdAt": "2026-09-23T20:10:00Z",
  "lockedUntil": null,
  "lockReason": null,
  "bannedAt": null,
  "banReason": null
}
```

`roles` is sorted by name. `lockedUntil` and `lockReason` are **null when the lock has run out**, not a date
in the past.

---

## `GET /api/users?search=&page=&pageSize=` - Admin or Moderator

People whose email, or first and last name, contains `search` (case-insensitive), newest first.

| Query | Default | Rule |
| :-- | :-- | :-- |
| `search` | none (everybody) | at most 255 characters |
| `page` | 1 | greater than 0 |
| `pageSize` | 12 | 1 to 50 |

```json
{ "items": [ { "id": "…", "email": "…", "roles": ["Customer"], "…": "…" } ], "page": 1, "pageSize": 12, "totalCount": 1 }
```

| Status | When |
| :-- | :-- |
| 200 | Always, for staff; an empty `items` when nothing matches |
| 400 | A paging or search rule broken |
| 401 | No token |
| 403 | Customer or seller |

---

## `PUT /api/users/{id}/roles/{role}` - Admin

Grant a role. **`Moderator` is the only value accepted.**

| Status | When |
| :-- | :-- |
| 200 | Granted, or already held (nothing written) |
| 400 | `role` is not `Moderator` - "Only the Moderator role can be granted here." |
| 401 | No token |
| 403 | Not an administrator (attribute) |
| 404 | "User not found." |
| 409 | "A banned account cannot be given a role." |

Side effects on a real change: `Security` / `RoleGranted` audit entry; `ModeratorGranted` notification with
link `/admin`. The person holds the role at their next sign-in or refresh.

## `DELETE /api/users/{id}/roles/{role}` - Admin

Revoke a role. `Moderator` only ("Only the Moderator role can be revoked here.").

| Status | When |
| :-- | :-- |
| 200 | Revoked, or not held (nothing written) |
| 400, 401, 403, 404 | As for the grant |

Side effects on a real change: `Security` / `RoleRevoked`; `ModeratorRevoked` notification, no link. The
session keeps the role in its current access token until the next refresh.

---

## `POST /api/users/{id}/lock` - Admin or Moderator

```json
{ "days": 3, "reason": "Spam in reviews" }
```

Checked in this order:

1. Validator: `days` 1 to 365; `reason` not blank, at most 500 characters → 400.
2. A caller who is not an administrator asking for more than 30 days → **403** "A moderator can lock an
   account for at most 30 days." - before the target is read, so the id's existence is not revealed.
3. Unknown id → 404 "User not found.".
4. `ModerationRules.EnsureMayStop`: own account → **409** "You cannot lock or ban your own account."; an
   administrator → **409** "An administrator cannot be locked or banned."; a moderator targeting a
   moderator → **403** "Only an administrator can lock a moderator.".

On success: `LockedUntil = now + days`, `LockReason` trimmed; `Moderation` / `AccountLocked`; then every
refresh token of the person revoked. 200 with the account. A lock on an already locked account replaces
it.

## `POST /api/users/{id}/unlock` - Admin or Moderator

No body. Clears `LockedUntil` and `LockReason` if set, recording `Moderation` / `AccountUnlocked`; otherwise
changes nothing. 200, or 404. **No rule on the target at this merge** - specs/050 (#121) added
`EnsureMayRelease`, with 409 and 403 cases. Unlocking does not lift a ban.

## `POST /api/users/{id}/ban` - Admin

```json
{ "reason": "Fraud" }
```

400 for a blank or over-long reason; 403 for a moderator (attribute); 404; the same two 409s as the lock.
On success: `BannedAt = now`, `BanReason`; `Moderation` / `AccountBanned`; every refresh token revoked.

## `POST /api/users/{id}/unban` - Admin

No body. Clears the ban if set, recording `Moderation` / `BanLifted`. 200, 403 for a moderator, or 404.

---

## Changed: `POST /api/auth/login` - anonymous

Unchanged for an active account. For a locked or banned one:

| Password | Status | Body |
| :-- | :-- | :-- |
| Wrong | 401 | "Invalid email or password." - the same as for an unknown email or any wrong password (#28) |
| Right, banned | **403** | `detail`: "This account is banned: {BanReason}" |
| Right, locked | **403** | `detail`: "This account is locked until {LockedUntil:yyyy-MM-dd HH:mm} UTC: {LockReason}" |

A ban wins when both hold. The refusal is recorded as `Security` / `SignInRefused`, actor the account
itself. Specs/049 (#130) later added `code`, `until` and `reason` extensions to this 403.

## Changed: `POST /api/auth/refresh` - anonymous, refresh cookie

A locked or banned account's refresh is **401** with the ordinary "not valid" message, whatever token the
`refreshToken` cookie presents - even one that escaped revocation. A granted or revoked role appears in the
refreshed response's `roles`, because refresh reads the roles from the database.

---

## Authorization

| Endpoint | Access |
| :-- | :-- |
| `GET /api/users` | `StaffRoles.Staff` (Admin or Moderator) |
| `POST /api/users/{id}/lock`, `/unlock` | `StaffRoles.Staff`; target rules in the handler |
| `PUT`, `DELETE /api/users/{id}/roles/{role}` | `RoleNames.Admin` |
| `POST /api/users/{id}/ban`, `/unban` | `RoleNames.Admin`; target rules in the handler for `ban` |

`UsersController` carries `[Authorize(Roles = StaffRoles.Staff)]` on the class and
`[Authorize(Roles = RoleNames.Admin)]` on the four admin actions; both attributes must pass.

## Gateway routes

Added to `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`, both to `identity-cluster`:

| Route | Path |
| :-- | :-- |
| `users-route` | `/api/users/{**catch-all}` |
| `users-root-route` | `/api/users` |
