# HTTP Contract: An audit log of who did what

> Written on 2026-09-27, after the feature merged (#93), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](../spec.md)

**Base**: Activity on `http://localhost:5063`, through the gateway at `/api/audit` (route
`audit-root-route`) and `/api/audit/{**catch-all}` (route `audit-route`), both on `activity-cluster`.
Health: `/api/activity/health`, rewritten to `/health`.

Every endpoint below is on `AuditController`, which carries `[Authorize(Roles = "Admin")]`. Errors are
RFC 7807 through `GlobalExceptionHandler`.

---

## `GET /api/audit` - Admin

A page of entries, newest first (`OccurredAt` descending, then `Id` descending).

**Query** (`GetAuditEntriesQuery`; all optional, combined with AND):

| Name | Meaning |
| :-- | :-- |
| `category` | One of `System`, `Security`, `User`, `Catalog`, `Order`, `Payment`, `Moderation` |
| `action` | Exact action name, e.g. `OrderPlaced` |
| `actor` | Part of the actor's email, case-insensitive (`ILIKE`) |
| `actorId` | The actor's id |
| `subjectType`, `subjectId` | Exact subject |
| `from`, `to` | Bounds on `OccurredAt`, both inclusive |
| `page` | At least 1; default 1 |
| `pageSize` | 1 to 100; default 20 |

**200**:

```json
{
  "items": [
    {
      "id": "0199...", "category": "Order", "action": "ParcelShipped",
      "actorId": "0199...", "actorEmail": "admin@example.com", "actorRole": "Admin",
      "subjectType": "Order", "subjectId": "0199...",
      "summary": "The shop's parcel shipped (VN123)", "service": "order",
      "occurredAt": "2026-09-23T19:40:00Z", "changeCount": 2
    }
  ],
  "page": 1, "pageSize": 20, "totalCount": 1
}
```

**400** - an unknown `category` ("Category must be one of: ..."), `page` below 1, `pageSize` outside
1-100, or `from` after `to`. **401** without a token. **403** for any role but Admin.

---

## `GET /api/audit/{id}` - Admin

One entry in full (`AuditEntryResponse`): the list fields (without `changeCount`) plus `recordedAt`,
`before` and `after` as JSON values (or `null`), and `changes`, the stored list of
`{ path, before, after }`.

**404** `Audit entry not found.` for an unknown id.

---

## `GET /api/audit/summary` - Admin

How many entries each category holds in a period - the numbers on the page's tabs.

**Query**: `from`, `to` (optional). **200**: `[{ "category": "Order", "count": 12 }, ...]`. A category
with no entries in the period is absent, not zero.

---

## Authorization

| Endpoint | Access |
| :-- | :-- |
| `GET /api/audit`, `/api/audit/{id}`, `/api/audit/summary` | `Admin` |
| `GET /api/activity/health` | Anonymous |

Bruno: `bruno/admin-audit/the audit log records the order it followed.yml`,
`bruno/admin-audit/the audit summary counts each category.yml`,
`bruno/security-checks/a customer cannot read the audit log is 403.yml`,
`bruno/security-checks/the audit log without a token is 401.yml`.

Later: specs/045 added `GET /api/audit/mine` (Admin, Moderator) to the same service.
