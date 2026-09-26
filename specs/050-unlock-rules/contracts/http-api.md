# HTTP Contract: Unlock rules

> Written on 2026-09-27, after the feature merged (#131), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature**: [spec.md](../spec.md)

One existing endpoint gains refusals. No route, request or success body changed; no message or gRPC contract
changed.

## `POST /api/users/{id}/unlock` - `Admin` or `Moderator` (`StaffRoles.Staff`)

Through the gateway: `http://localhost:5000/api/users/{id}/unlock`. No body.

**Responses**:

| Status | When | Body |
| :--- | :--- | :--- |
| `200` | Allowed - the lock is cleared, or there was none | `UserAdminResponse` (unchanged): `id`, `email`, `firstName`, `lastName`, `roles`, `createdAt`, `lockedUntil` (null), `lockReason` (null), `bannedAt`, `banReason` |
| `401` | No or invalid token | (unchanged) |
| `403` | Caller is not staff | (unchanged, from the attribute) |
| `403` | **New.** A moderator unlocking a moderator | ProblemDetails, `detail` "Only an administrator can unlock a moderator." |
| `403` | **New.** A moderator lifting a lock with more than 30 days to run | ProblemDetails, `detail` "Only an administrator can lift a lock with more than 30 days to run." |
| `404` | No such user | (unchanged) |
| `409` | **New.** The target is the caller - locked or not | ProblemDetails, `detail` "You cannot unlock your own account." |

### Who may unlock whom

| Caller \ target | Themselves | A moderator | Lock with ≤ 30 days to run | Lock with > 30 days to run |
| :-- | :-- | :-- | :-- | :-- |
| Administrator | 409 | allowed | allowed | allowed |
| Moderator | 409 | **403** | allowed | **403** |

A refused unlock changes nothing and records no audit entry.

## Bruno

`bruno/admin-users/a moderator cannot unlock their own account.yml` (`seq: 12`): `POST
{{baseUrl}}/api/users/{{modId}}/unlock` with `{{modToken}}` → 409 whose `detail` contains "your own account".
