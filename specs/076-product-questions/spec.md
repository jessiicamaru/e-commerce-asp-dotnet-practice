# Feature Specification: A shopper asks a seller about a product

> Completed on 2026-09-27, after the feature merged (#160), from the code at that merge, the pull request and docs/features/product-questions.md.

**Feature Branch**: `076-product-questions` | **Created**: 2026-09-26 | **Issue**: #110 (closes it)

**Status**: Merged (#160, 2026-09-26)

**Input**: Issue #110, "feat(catalog): nobody can ask a seller about a product": questions on a product by any
signed-in customer, answers by its seller (or staff for the shop's own); a public list on the product page; the
seller notified of a new question and the asker of an answer; moderators hide a question or answer, like reviews.
Acceptance: "Only the product's seller can answer as the seller; somebody else's answer attempt is a 404."

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

**Why this priority**: Nothing else in the feature exists without a question to answer or moderate, and it is the
issue's first line. It shares P1 with US2 because the issue asks for the two together: a question and its seller's
answer.

**Independent Test**: As a customer, ask about an approved seller's product; read the product's questions
anonymously and find it first, signed with the customer's first name; the seller's notifications hold
`NewQuestion`. Asking about a product awaiting review is a 404, and the seller asking about their own is a 403.

**Acceptance Scenarios**:

1. **Given** an approved product of a seller, **When** a signed-in customer asks "Does it come with the charger?",
   **Then** the question appears first on the product's public list, signed with their first name, with no answer,
   and the seller is notified.
2. **Given** a seller's product that is still waiting for a moderator, **When** a customer asks about it, **Then**
   the answer is `404 Product not found.` - the same as a product that does not exist.
3. **Given** a seller who also holds `Customer`, **When** they ask about their own product, **Then** they get
   `403 You cannot ask about your own product.` and nothing is stored.
4. **Given** a product the shop lists itself, **When** a customer asks about it, **Then** the question is stored
   and nobody in particular is notified; it waits in staff's queue.
5. **Given** no token, **When** somebody asks, **Then** the answer is 401.

---

### US2 - Answer (P1)

The product's seller answers, and an answer can be rewritten. For the shop's own products (no seller), staff
answer.
- **Anyone else is a 404**, the same "not yours" as the rest of the seller surface (specs/027). That includes an
  administrator on a seller's product: answering as the seller is the seller's to do, and moderation is hiding,
  not speaking for them.
- The asker is told once, on the first answer, not on every edit.
- A seller has a queue of their unanswered questions at `/shop/questions`, and staff have the shop's own at
  `/admin/questions`.

**Why this priority**: The issue's one acceptance criterion is about who may answer. An answer is the only reason
to ask.

**Independent Test**: Ask about a seller's product; another seller, an administrator and a made-up question id all
get the same `404 Question not found.`; the product's seller answers and the asker is notified once; a rewrite
replaces the text and notifies nobody.

**Acceptance Scenarios**:

1. **Given** a question on seller A's product, **When** seller B, an administrator, or anybody with a made-up
   question id tries to answer, **Then** each gets `404 Question not found.`, indistinguishable from the others.
2. **Given** the same question, **When** seller A answers, **Then** the answer shows on the product page and the
   asker is notified once.
3. **Given** an answered question, **When** seller A answers again, **Then** the text is replaced, it reads as
   edited, and nobody is notified.
4. **Given** ten first answers sent at the same moment, **When** they complete, **Then** the question has one
   answer and the asker has exactly one notice.
5. **Given** a question on the shop's own product, **When** a moderator answers, **Then** it is answered; **when**
   a seller tries, **then** 404.
6. **Given** seller A with one unanswered and one answered question, **When** they open their queue, **Then**
   *unanswered* lists only the first (with the product's name), *answered* only the second, and nothing of other
   sellers or the shop.

---

### US3 - Moderate (P2)

Staff hide a question, which takes it off the page with its answer, or hide only the answer, which makes the
question read as unanswered. Either needs a reason, and either can be restored.
- The author is told why: the asker when a question is hidden, the answerer when an answer is hidden.
- A hidden answer cannot be rewritten until staff restore it. Otherwise hiding would last only until the next
  edit.
- Every decision is in the audit log.

**Why this priority**: Anything public needs a way to take it down - the issue's third line, "like reviews" - but it
matters only once questions and answers exist.

**Independent Test**: Hide a question with a reason: it leaves the public list, the asker is told why, and a second
hide is a 409; restore it and it is back. Hide an answer: the question reads as unanswered, the answerer is told,
and the seller's rewrite is a 409 until staff restore it.

**Acceptance Scenarios**:

1. **Given** two visible questions, **When** a moderator hides the older with reason "Off topic", **Then** the
   public list holds only the newer, the asker is notified with the reason, the staff list of hidden questions
   holds it, and hiding it again is a 409.
2. **Given** a hidden question, **When** it is restored, **Then** the public list holds both again, and restoring
   it a second time is a 409.
3. **Given** an answered question, **When** a moderator hides only the answer, **Then** the public list shows the
   question with no answer and no answer time, and the seller is notified with the reason.
4. **Given** a hidden answer, **When** the seller rewrites it, **Then** they get
   `409 This question or its answer was hidden by a moderator.`; after staff restore it, the rewrite succeeds.
5. **Given** a hidden question, **When** its seller tries to answer it, **Then** 409 and no notice.
6. **Given** any of these decisions, **When** it is made, **Then** the audit log holds one entry for it, in the
   Moderation category, with the reason.

---

### Edge Cases

- **Two first answers at once.** The first-answer guard lets one through; the other becomes a rewrite. One notice.
- **Two moderators at once.** One hides; the other's guarded statement matches nothing and gets a 409.
- **Answering your own question.** Possible only when the asker is also the product's seller (who cannot ask) or
  staff on the shop's own; the code sends no `QuestionAnswered` to somebody answering themselves.
- **A token from before `given_name` existed.** The asker is signed with the email's first letter and a full stop,
  or `?` with no email.
- **A product deleted.** Its questions cascade away with it.
- **A product taken off the shelf after questions were asked.** At the merge its questions stayed readable by
  address and its seller could still answer (research D4). Since specs/081 (#169) the public list is a 404 for
  anybody but its seller and staff; answering is unchanged.
- **Hiding an answer that does not exist yet**, or is already hidden: 409 `This question has no visible answer to
  hide.`
- **A hidden question with an answer.** Restoring the question brings the answer back as it was; the answer's own
  hidden state is separate.

## Requirements

- **FR-001** `product_questions`:
  - the question: `Id`, `ProductId` (cascade), `AskerId`, `AskerName`, `Body` (at most 1000 characters),
    `CreatedAt`, and `HiddenAt` / `HiddenReason` / `HiddenBy`;
  - the answer: `Answer` (at most 2000 characters), `AnsweredBy`, `AnsweredAt`, `AnswerUpdatedAt`, and
    `AnswerHiddenAt` / `AnswerHiddenReason` / `AnswerHiddenBy`.
  - One answer per question: it is the seller's, not a thread.
  - *Completed on 2026-09-27 from the code:* `AskerName` is at most 100 characters and both hidden reasons at most
    500. See [data-model.md](./data-model.md).
- **FR-002** Every change is one guarded statement, run in its transaction with its audit entry and notice (the
  `stage` pattern):
  - the first answer is `WHERE "AnsweredAt" IS NULL`; a rewrite is `WHERE "AnsweredAt" IS NOT NULL AND
    "AnswerHiddenAt" IS NULL`;
  - a hide is `WHERE "HiddenAt" IS NULL`, and a restore is `IS NOT NULL`.
  - Two people at once: exactly one decides and the other gets a 409 (or, for answering, becomes the rewrite).
  - *Completed on 2026-09-27 from the code:* both answer statements also carry `AND "HiddenAt" IS NULL`, which is
    what makes a hidden question unanswerable (409). The answer's hide and restore guard on `"AnswerHiddenAt"` the
    same way (hide also requires `"AnsweredAt" IS NOT NULL`). The full table is in [data-model.md](./data-model.md).
- **FR-003** The endpoints:

  | Method | Path | Who | What |
  | :-- | :-- | :-- | :-- |
  | `GET` | `/api/products/{id}/questions` | Anonymous | The visible questions, paged, each with its visible answer. |
  | `POST` | `/api/products/{id}/questions` | Customer | Ask. |
  | `PUT` | `/api/questions/{id}/answer` | Seller or Staff | Answer or rewrite. Not theirs is 404. |
  | `GET` | `/api/questions/to-answer?answered=` | Seller or Staff | A seller's own products, or for staff the shop's own. |
  | `GET` | `/api/questions?hidden=` | Staff | Every visible or every hidden question. |
  | `POST` | `/api/questions/{id}/hide`, `/restore`, `/answer/hide`, `/answer/restore` | Staff | Moderate. |

  *Completed on 2026-09-27 from the code:* `?hidden=true` returns every question with **something** hidden - the
  question, or only its answer - which is what the admin page's *Hidden* tab needs to offer "restore answer". Full
  status codes and bodies are in [contracts/http-api.md](./contracts/http-api.md).
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

*Added on 2026-09-27, stating as requirements what the user stories and the code already hold:*

- **FR-007** Every change - asking, the first answer, a rewrite, and each hide and restore - MUST be recorded in the
  audit log in the transaction that makes it. Moderation decisions are in the Moderation category with their reason.
- **FR-008** The asker's id and signature, and the answerer's id, MUST come from the access token. No request names
  a user, a seller or a display name.
- **FR-009** Only a product that is approved and active MAY be asked about; anything else MUST be the same 404 as a
  product that does not exist. A caller MUST NOT ask about a product they sell (403, in words).
- **FR-010** Only the product's seller MAY answer a question on a seller's product, and only staff on the shop's own.
  Every other caller - an administrator or moderator on a seller's product included - MUST get the same
  `404 Question not found.` as a question that does not exist.
- **FR-011** The asker MUST be notified once, on the first answer; a rewrite MUST notify nobody, and concurrent first
  answers MUST produce one notice.
- **FR-012** A hidden question MUST NOT be answerable, and a hidden answer MUST NOT be rewritable, until staff restore
  it (409).
- **FR-013** The public list MUST leave out hidden questions and MUST show a hidden answer as no answer; it MUST NOT
  carry hiding reasons or who hid.
- **FR-014** A seller's queue MUST hold only the visible questions on their own products; staff's queue only those on
  the shop's own. Unanswered comes oldest first.

## Key Entities

- **Product question**: one shopper's question about one product - who asked (id and first name), the words, when -
  and its one answer: the words, who last wrote them, when first answered and when last changed. Each part can be
  hidden by staff with a reason, recording who and when, and restored. Many per product; deleted with the product.

## Success Criteria

- **SC-001**: 100% of answer attempts by anybody other than the product's seller (or staff, for the shop's own) are
  refused with the one 404 a made-up question id gets - verified for another seller, an administrator and a made-up
  id (`ProductQuestionTests`, Bruno `an administrator answering for a seller is 404`).
- **SC-002**: Ten simultaneous first answers to one question produce exactly one answer notice to the asker.
- **SC-003**: A hidden question never appears in the public list, and a hidden answer never appears in it, until
  restored; a hidden answer's rewrite is refused every time until then.
- **SC-004**: Every hide and restore is one audit entry; a second identical decision is a 409 and writes nothing.
- **SC-005**: No question can be stored against a product that is not on sale, or by that product's own seller.

## Assumptions

- Signed-in customers are trusted to ask in good faith; staff hiding after the fact is the moderation, as for
  reviews. There is no pre-moderation of questions.
- A seller's own products are identified by `products.SellerId`, as everywhere since specs/027; the shop's own have
  none.
- The asker's first name is the token's `given_name` claim added for reviews (specs/046).
- Staff are Admin or Moderator (specs/043); both answer for the shop and both moderate.
- The notices are worded by the storefront from the kind and data (specs/042); the product name in the data is the
  default-language name.

## Out of scope

- Several answers per question, or answers from other shoppers.
- The asker editing or deleting their question.
- Votes, like "was this helpful".
- Rate limiting beyond the gateway's.
