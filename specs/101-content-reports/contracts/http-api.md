# HTTP contract: Shoppers report a review, a question or a product

Through the gateway. New routes are `catalog-reports-route` (`/api/reports/{**catch-all}`) and
`catalog-reports-root-route` (`/api/reports`), both → `catalog-cluster`.

## `POST /api/reports`: signed in (Catalog)

```json
{ "targetType": "Review", "targetId": "0199...", "reason": "Spam", "details": "Links to another shop" }
```

- `201` returns `{ "id", "targetType", "targetId", "reason", "createdAt" }`.
- `400`:
  - an unknown `targetType` (Review, Question or Product);
  - an unknown `reason` (Spam, Offensive, Misleading, Counterfeit or Other);
  - `details` over 500 characters.
- `404`: the review, question or product is unknown, hidden or off the shelf. The message is the one its own page gives.
- `409`:
  - `You cannot report your own review.` (or question, or product);
  - `You have already reported this; staff will look at it.`
- `401` without a token.

## `GET /api/reports?pageNumber=1&pageSize=12`: Admin, Moderator (Catalog)

```json
{
  "items": [{
    "targetType": "Review", "targetId": "0199...", "productId": "0199...", "productName": "Fujifilm X-T5",
    "excerpt": "Buy it cheaper elsewhere", "reportCount": 2,
    "reasons": { "Spam": 1, "Misleading": 1 }, "details": ["Links to another shop"],
    "firstReportedAt": "...", "lastReportedAt": "..."
  }],
  "pageNumber": 1, "totalPages": 1, "totalCount": 1, "hasPreviousPage": false, "hasNextPage": false
}
```

- Open reports only, one row per thing, ordered by `reportCount` descending and then `firstReportedAt`.
- `details` holds the latest three non-empty entries.
- `excerpt` is the review or question text as it reads now, or the product's name.

## `POST /api/reports/{targetType}/{targetId}/dismiss`: Admin, Moderator (Catalog)

- `204`: every open report of the thing is `Dismissed`, each reporter is told, and the decision is audited.
- `409` `Nothing reported about this is waiting.`
- `400` for an unknown `targetType`.

## Acting on a report

Acting uses the existing endpoints, unchanged in shape. Each now closes the thing's open reports as `Actioned`:

- `POST /api/reviews/{id}/hide`
- `POST /api/questions/{id}/hide` and `POST /api/questions/{id}/answer/hide`
- `POST /api/products/{id}/take-down`
