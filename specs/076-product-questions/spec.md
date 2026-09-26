# Feature Specification: A shopper asks a seller about a product

**Feature Branch**: `076-product-questions` | **Created**: 2026-09-26 | **Issue**: #110 (closes it)

## Why

A shopper with a question like "does this come with the charger?" has nowhere to ask it. Reviews (specs/046) are
only for somebody who received the product, and they are opinions, not answers.

## User Scenarios

### US1 - Ask (P1)

A signed-in customer asks a question on the page of a product that is on sale.
- The question is public on the product page, newest first, signed with the asker's first name from the token,
  the way a review is.
- A product that is not on sale is the public lookup's 404.
- A seller does not ask about their own product (403, in words).
- The product's seller is told there is a new question. The shop's own products have no one person to tell:
  staff see them in their queue.

### US2 - Answer (P1)

The product's seller answers, and an answer can be rewritten. For the shop's own products (no seller), staff
answer.
- **Anyone else is a 404**, the same "not yours" as the rest of the seller surface (specs/027). That includes an
  administrator on a seller's product: answering as the seller is the seller's to do, and moderation is hiding,
  not speaking for them.
- The asker is told once, on the first answer, not on every edit.
- A seller has a queue of their unanswered questions at `/shop/questions`, and staff have the shop's own at
  `/admin/questions`.

### US3 - Moderate (P2)

Staff hide a question, which takes it off the page with its answer, or hide only the answer, which makes the
question read as unanswered. Either needs a reason, and either can be restored.
- The author is told why: the asker when a question is hidden, the answerer when an answer is hidden.
- A hidden answer cannot be rewritten until staff restore it. Otherwise hiding would last only until the next
  edit.
- Every decision is in the audit log.

## Requirements

- **FR-001** `product_questions`:
  - the question: `Id`, `ProductId` (cascade), `AskerId`, `AskerName`, `Body` (at most 1000 characters),
    `CreatedAt`, and `HiddenAt` / `HiddenReason` / `HiddenBy`;
  - the answer: `Answer` (at most 2000 characters), `AnsweredBy`, `AnsweredAt`, `AnswerUpdatedAt`, and
    `AnswerHiddenAt` / `AnswerHiddenReason` / `AnswerHiddenBy`.
  - One answer per question: it is the seller's, not a thread.
- **FR-002** Every change is one guarded statement, run in its transaction with its audit entry and notice (the
  `stage` pattern):
  - the first answer is `WHERE "AnsweredAt" IS NULL`; a rewrite is `WHERE "AnsweredAt" IS NOT NULL AND
    "AnswerHiddenAt" IS NULL`;
  - a hide is `WHERE "HiddenAt" IS NULL`, and a restore is `IS NOT NULL`.
  - Two people at once: exactly one decides and the other gets a 409 (or, for answering, becomes the rewrite).
- **FR-003** The endpoints:

  | Method | Path | Who | What |
  | :-- | :-- | :-- | :-- |
  | `GET` | `/api/products/{id}/questions` | Anonymous | The visible questions, paged, each with its visible answer. |
  | `POST` | `/api/products/{id}/questions` | Customer | Ask. |
  | `PUT` | `/api/questions/{id}/answer` | Seller or Staff | Answer or rewrite. Not theirs is 404. |
  | `GET` | `/api/questions/to-answer?answered=` | Seller or Staff | A seller's own products, or for staff the shop's own. |
  | `GET` | `/api/questions?hidden=` | Staff | Every visible or every hidden question. |
  | `POST` | `/api/questions/{id}/hide`, `/restore`, `/answer/hide`, `/answer/restore` | Staff | Moderate. |

- **FR-004** New kinds in `notification-kinds.json`, worded in both languages: `NewQuestion {product}`,
  `QuestionAnswered {product}`, `QuestionHidden {product, reason}` and `AnswerHidden {product, reason}`.
- **FR-005** Storefront:
  - a Questions section on the product page (the list, the ask form, and an answer box for whoever may answer);
  - `/shop/questions`;
  - `/admin/questions`, with the tabs *To answer*, *Visible* and *Hidden* and hide and restore for both the
    question and the answer;
  - Vitest tests.
- **FR-006** Bruno covers asking, the list, the seller answering, someone else answering (404), the seller's
  queue, and 401/403. The seller's requests go in `seller/`.

## Out of scope

- Several answers per question, or answers from other shoppers.
- The asker editing or deleting their question.
- Votes, like "was this helpful".
- Rate limiting beyond the gateway's.
