# HTTP contract: Staff see a person's moderation history

Through the gateway's existing `audit-route` (`/api/audit/{**catch-all}` → Activity). No gateway change.

## `GET /api/audit/people/{userId}?page=1&pageSize=20`: Admin, Moderator (Activity)

`200`:

```json
{
  "items": [
    {
      "id": "0199...",
      "action": "AccountLocked",
      "actorEmail": "mod@example.test",
      "actorRole": "Moderator",
      "subjectType": "User",
      "subjectId": "0199...",
      "summary": "lan@example.test locked for 3 day(s)",
      "reason": "Spam in reviews",
      "occurredAt": "2026-09-27T01:40:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

- The items are Moderation entries only, newest first. The route takes no category parameter.
- `reason` is null when the decision carried none, as with an unlock.
- There are no `before`, `after` or `changes` fields.
- An unknown person returns an empty page, not a 404.
- `400` when `page` is less than 1 or `pageSize` is outside 1-100.
- `401` without a token; `403` for anyone who is not Admin or Moderator.
