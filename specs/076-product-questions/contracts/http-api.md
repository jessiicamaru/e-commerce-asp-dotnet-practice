# HTTP Contract: Product questions

> Written on 2026-09-27, after the feature merged (#160), from the code at that merge, the pull request and docs/features/product-questions.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

Nine endpoints on Catalog, in `QuestionsController` (`[Route("api")]`). Through the gateway on `:5000`:
`/api/products/{id}/questions` rides Catalog's existing products route; `/api/questions` and
`/api/questions/{**catch-all}` are two new routes, `catalog-questions-root-route` and `catalog-questions-route`, on
`catalog-cluster`.

Errors are RFC 7807 ProblemDetails through `Ecommerce.Shared`'s `GlobalExceptionHandler` - see
[error-handling-and-shared-building-block.md](../../../docs/architecture/error-handling-and-shared-building-block.md).
A `400` from a validator carries an `errors` extension. `ForbiddenException`'s message is shown to the caller.

"Staff" is `StaffRoles.Staff` = `Admin,Moderator`.

---

## The response: `QuestionResponse`

Every endpoint returns this record, alone or in a page. Two shapes, chosen by the handler:

**Public** (`QuestionResponse.Public`) - the product page's list and the reply to asking:

```json
{
  "id": "0199...",
  "productId": "0199...",
  "askerName": "Mai",
  "body": "Does it come with the charger?",
  "createdAt": "2026-09-26T10:00:00Z",
  "answer": "Yes, in the box.",
  "answeredAt": "2026-09-26T10:05:00Z",
  "answerEdited": false,
  "productName": null,
  "hiddenAt": null,
  "hiddenReason": null,
  "answerHiddenAt": null,
  "answerHiddenReason": null
}
```

A **hidden answer** comes back as no answer: `answer` and `answeredAt` null, `answerEdited` false. No reasons, no
product name, no ids of who answered or hid.

**Full** (`QuestionResponse.Full`) - the answer endpoint, both queues and every moderation endpoint: the same fields
with `productName`, `hiddenAt`, `hiddenReason`, `answerHiddenAt` and `answerHiddenReason` filled from the row.

`answerEdited` is true when the answer was rewritten more than a second after it was first written.

A page is Catalog's `PaginatedList`:
`{ "items": [...], "pageNumber": 1, "totalPages": 1, "totalCount": 1, "hasPreviousPage": false, "hasNextPage": false }`.

---

## `GET /api/products/{productId}/questions` - anonymous

A product's visible questions, **newest first**. Query: `pageNumber` (default 1, `> 0`), `pageSize` (default 12,
1 to 50) - out of range is `400`. Items are the public shape.

| Status | When |
| :--- | :--- |
| `200` | The page. At the merge, an unknown product id was also `200` with an empty page |
| `400` | Paging out of range |
| `404 Product not found.` | **Since specs/081 (#169), not at the merge**: the product does not exist, or it is off the shelf and the caller is neither its seller nor staff (`ProductReview.MaySee`). See research D4 |

---

## `POST /api/products/{productId}/questions` - `Customer`

Ask. Body: `{ "body": "Does it come with the charger?" }` - trimmed, not blank, at most 1000 characters. Nothing
names the asker: the id and the signature (`askerName`) come from the token (research D8).

| Status | When |
| :--- | :--- |
| `200` | The question, public shape. The product's seller is sent `NewQuestion`; the shop's own product notifies nobody |
| `400` | `Question is required.` or longer than 1000 |
| `401` | No token |
| `403` | The token holds no `Customer` role (the attribute), or the caller is the product's seller: `You cannot ask about your own product.` |
| `404 Product not found.` | Missing, not approved (`!IsListed`), or inactive - the public lookup's rule (specs/045) |

---

## `PUT /api/questions/{id}/answer` - `Seller`, `Admin`, `Moderator`

Answer, or rewrite the answer. Body: `{ "answer": "Yes, in the box." }` - trimmed, not blank, at most 2000.

⚠️ The attribute lets a seller and staff in; **which** question they may answer is the handler's to decide from the
product's row (research D2):

- a seller's product: only that seller;
- the shop's own product (`SellerId` null): only staff;
- anybody else - another seller, **an administrator or moderator on a seller's product** - gets the same `404` as a
  made-up id.

| Status | When |
| :--- | :--- |
| `200` | Full shape, after the first answer (the asker is sent `QuestionAnswered`) or a rewrite (nobody is told) |
| `400` | `Answer is required.` or longer than 2000 |
| `401` / `403` | No token / none of the three roles |
| `404 Question not found.` | The question does not exist, or the caller may not answer it |
| `409 This question or its answer was hidden by a moderator.` | The question is hidden, or its answer is (locked until restored, research D3) |

Whether a listing is on sale is **not** checked here: a seller can answer a question on a product taken off the
shelf (research D4).

---

## `GET /api/questions/to-answer?answered=` - `Seller`, `Admin`, `Moderator`

What the caller answers for: for a seller, the visible questions on **their own** products; for staff, those on
**the shop's own** products. Nobody gets somebody else's queue - there is no seller id in the request.

Query: `answered` (default `false`), `pageNumber` (default 1), `pageSize` (default 12). Unanswered comes oldest
first, answered newest first (by first answer). Items are the full shape, with `productName`.

`200`; `401` / `403` as above. The paging parameters of this and the next endpoint have no validator.

---

## `GET /api/questions?hidden=` - Staff

For moderators: `hidden=false` (default) is every visible question; `hidden=true` is every question with something
hidden - the question or only its answer. Newest first, full shape with the reasons and `productName`.

`200`; `401`; `403` for anybody not staff (Bruno: `a customer cannot read the staff questions is 403`).

---

## `POST /api/questions/{id}/hide` - Staff

Body: `{ "reason": "Off topic" }` - trimmed, not blank, at most 500. The question leaves the product page with its
answer; its asker is sent `QuestionHidden` with the reason.

`200` full shape; `400 Reason is required.`; `404 Question not found.`; `409 This question is already hidden.`

## `POST /api/questions/{id}/restore` - Staff

No body. `200`; `404`; `409 This question is not hidden.`

## `POST /api/questions/{id}/answer/hide` - Staff

Body: `{ "reason": "..." }`, as above. The question stays and reads as unanswered; the answer is locked until
restored; whoever last wrote it (`AnsweredBy`) is sent `AnswerHidden`.

`200`; `400`; `404`; `409 This question has no visible answer to hide.` (no answer yet, or already hidden)

## `POST /api/questions/{id}/answer/restore` - Staff

No body. `200`; `404`; `409 This answer is not hidden.`

---

## Authorization summary

| Endpoint | Access |
| :--- | :--- |
| `GET /api/products/{id}/questions` | Anonymous (since #169: off the shelf, its seller and staff only) |
| `POST /api/products/{id}/questions` | `Customer` |
| `PUT /api/questions/{id}/answer` | `Seller,Admin,Moderator` at the door; the product's seller, or staff for the shop's own, in the handler |
| `GET /api/questions/to-answer` | `Seller,Admin,Moderator`, scoped by the token |
| `GET /api/questions`, `POST .../hide`, `.../restore`, `.../answer/hide`, `.../answer/restore` | Staff |

No endpoint accepts a user id, a seller id or a name (Principle IV).
