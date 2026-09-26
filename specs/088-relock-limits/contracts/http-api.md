# HTTP Contract: Locking again obeys the unlock rule

**Feature**: [spec.md](../spec.md)

One existing endpoint gains one refusal. No route, request or success body changes; no message or gRPC
contract changes.

## `POST /api/users/{id}/lock` - `Admin` or `Moderator` (`StaffRoles.Staff`)

Through the gateway: `http://localhost:5000/api/users/{id}/lock`.

```json
{ "days": 1, "reason": "Shorter" }
```

**Responses**:

| Status | When | Body |
| :--- | :--- | :--- |
| `200` | Allowed | `UserAdminResponse` (unchanged) |
| `400` | `days` outside 1-365, or no reason | (unchanged) |
| `401` | No or invalid token | (unchanged) |
| `403` | Caller is not staff; a moderator asking for more than 30 days; a moderator locking a moderator | (unchanged) |
| `403` | **New.** A moderator whose lock would end sooner than one with more than 30 days still to run | ProblemDetails, `detail` "Only an administrator can shorten a lock with more than 30 days to run." |
| `404` | No such user | (unchanged) |
| `409` | Self, or an administrator | (unchanged) |

### Who may re-lock what

| Caller \ lock in place | None or run out | Ends later than the new one, ≤ 30 days to run | Ends later, > 30 days to run | Ends sooner (extending) |
| :-- | :-- | :-- | :-- | :-- |
| Administrator | allowed | allowed | allowed | allowed |
| Moderator | allowed | allowed | **403** | allowed |

## Bruno

In `bruno/admin-users/` (the requests after them renumbered by 3):

| seq | Request | Expects |
| :-- | :-- | :-- |
| 17 | `register an account for a long lock` | 200; sets `longLockId` |
| 18 | `an administrator locks it for 300 days` | 200 |
| 19 | `a moderator cannot shorten the lock by locking again` | 403, `detail` contains "shorten" |

The account is its own: locking ends every session, and later folders need the customer's.
