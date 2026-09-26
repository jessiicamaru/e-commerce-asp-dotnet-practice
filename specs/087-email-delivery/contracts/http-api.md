# HTTP Contract: Email delivery

> Written on 2026-09-27, after the feature merged (#179), from the code at that merge, the pull request and
> docs/features/email.md.

**Feature**: [spec.md](../spec.md)

Two new Identity endpoints, both `Admin` only (`[Authorize(Roles = "Admin")]` on `EmailsController`), through the
gateway on `:5000`. No message or gRPC contract changed; the audit entry travels as the existing
`AuditEntryRecorded` (specs/041).

Errors follow the project's RFC 7807 shape through `GlobalExceptionHandler`.

---

## `GET /api/emails`

Query:

| Parameter | Default | Rule |
| :--- | :--- | :--- |
| `status` | `Failed` | `Pending`, `Sent` or `Failed`, any case - else `400` `Status is Pending, Sent or Failed.` |
| `search` | none | at most 255; matches recipients whose address contains it, case-insensitively |
| `page` | 1 | > 0 |
| `pageSize` | 12 | 1-50 |

`200`:

```json
{
  "items": [
    {
      "id": "0193...",
      "recipientId": "0192...",
      "recipientEmail": "lan@example.com",
      "template": "OrderPaid",
      "language": "vi",
      "status": "Failed",
      "attempts": 12,
      "lastError": "Connection refused",
      "createdAt": "2026-09-26T10:00:00Z",
      "nextAttemptAt": "2026-09-26T21:00:00Z",
      "sentAt": null,
      "canRetry": true
    }
  ],
  "page": 1,
  "pageSize": 12,
  "totalCount": 1
}
```

Newest first. **No field carries the email's data.** `recipientEmail` is null when the account is gone. `canRetry` is
true only for a `Failed` email that is not `PasswordReset` or `EmailConfirmation`.

| Caller | Status |
| :--- | :--- |
| Administrator | `200` |
| Moderator | `403` (Bruno `admin-users/a moderator cannot read the email log is 403`) |
| No token | `401` (Bruno `security-checks/the email log without a token is 401`) |

## `POST /api/emails/{id}/retry`

No body. `200` with the email as above, now `"status": "Pending"`, `"attempts": 0`, `"lastError": null`,
`"canRetry": false`.

| Situation | Status | Detail |
| :--- | :--- | :--- |
| Failed, not a link | `200` | - |
| `PasswordReset` or `EmailConfirmation`, any state | `409` | `A reset or confirmation link expires; the person asks for a new one instead.` |
| Pending or Sent (including a second retry) | `409` | `This email is <state>; only a failed one can be sent again.` |
| Unknown id | `404` | `Email not found.` |
| Moderator / no token | `403` / `401` | - |

## Gateway

Two routes to `identity-cluster` in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`: `emails-root-route`
(`/api/emails`) and `emails-route` (`/api/emails/{**catch-all}`) - a root route beside each catch-all, as the
gateway's other Identity and Catalog routes are written.
