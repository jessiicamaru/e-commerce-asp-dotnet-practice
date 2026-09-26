# Phase 0 Research: A shopper asks a seller about a product

> Written on 2026-09-27, after the feature merged (#160), from the code at that merge, the pull request and docs/features/product-questions.md.

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Issue**: #110

D1 to D4 were written in [plan.md](./plan.md)'s "Research" section when the feature was planned, and are kept there
word for word. They are repeated here with their alternatives. D5 to D11 are decisions the code and the pull request
make without a heading of their own; they are recorded so the next reader does not have to rediscover them. Where
the record names no rejected alternative, this file says "not recorded" rather than inventing one.

---

## D1 - One answer per question, and it is the seller's

**Decision**: A question has at most one answer, stored on the question's own row. A second write by whoever may
answer **replaces** it (a rewrite), and the response marks it `answerEdited`.

**Rationale**: A thread invites shoppers to answer each other, which is a second moderation surface and not what the
issue asks for (#110 asks for "answers by its seller (or staff for the shop's own)"). The answer is written as the
shop, so a rewrite replaces it rather than adding a second voice.

**Alternatives considered**:

- **A thread of replies.** Rejected for the reason above: every reply is another thing staff must be able to hide,
  and the issue did not ask for a conversation.
- **Answers from other shoppers.** Rejected with the thread; listed under Out of scope in [spec.md](./spec.md).
- **Votes ("was this helpful").** Out of scope; nothing to rank when there is one answer.

---

## D2 - An administrator does not answer on a seller's product

**Decision**: `AnswerableAsync` in `QuestionHandlers` decides who answers from the product's row:

- a product with a seller (`SellerId` set): **only that seller**;
- the shop's own product (`SellerId` null): **only staff** (Admin or Moderator);
- anybody else - **an administrator on a seller's product included** - gets `404 Question not found.`, the same
  message a question that does not exist gets.

**Rationale**: `SellerOwnership` lets an administrator through every product write, because moderation is the job.
An answer, though, is published as the seller's words, so staff answer only for the shop's own products. They
moderate everyone's (D3). The 404 rather than a 403 is the rule of the seller surface since specs/027: a 403
confirms that the id is real and belongs to somebody. The issue's one acceptance line is this rule: "Only the
product's seller can answer as the seller; somebody else's answer attempt is a 404."

**Alternatives considered**:

- **Reuse `SellerOwnership`**, as the nine product write handlers do. Rejected: it passes an administrator, which
  would let staff put words in a seller's mouth on the seller's own product page. The PR's mutation "letting staff
  answer a seller's product" turned the admin-404 test red, so this is guarded.
- **A 403 for "not yours".** Rejected: it would distinguish "exists, not yours" from "does not exist" (specs/027).

---

## D3 - A hidden answer is locked until restored

**Decision**: The rewrite statement's guard includes `"AnswerHiddenAt" IS NULL`. When staff have hidden an answer,
the seller's next `PUT .../answer` matches no row for the first answer (it exists) nor for the rewrite (it is
hidden), and the handler answers **409** `This question or its answer was hidden by a moderator.` Staff restore it
through `POST /api/questions/{id}/answer/restore`, after which the seller may rewrite again.

**Rationale**: Otherwise a seller undoes the moderator with an edit - hiding would last only until the next edit.

**Alternatives considered**:

- **A rewrite clears the hide** (the edited text is new, so show it). Rejected for the reason above.
- **A rewrite is allowed but stays hidden.** Not recorded as considered.

---

## D4 - A question on a product that is no longer on sale

**Decision (at the merge)**: It stays readable by the public list (the product page is itself a 404 for the public),
and the seller can still answer it.

> **Superseded in part on 2026-09-26 by specs/081 (#169, issue #166).** The public list now follows the public
> lookup's rule: `GetProductQuestionsQuery` loads the product and answers `404 Product not found.` unless
> `ProductReview.MaySee(product, caller)` - on the shelf, or the caller is its seller or staff. The second half
> still holds: `AnswerableAsync` does not ask whether the product is listed, so its seller (or staff, for the
> shop's own) can still answer. See [specs/081-unlisted-product-reads](../081-unlisted-product-reads/).

**Rationale (at the merge)**: The product page was the gate - a shopper could not reach the list without it. #166
found that the list's own address was reachable directly, which is why specs/081 moved the check into the query.

**Alternatives considered**: not recorded.

---

## D5 - Every change after the insert is one guarded statement, with its announcements staged inside it

**Decision**: `IProductQuestionRepository` exposes `TryAnswerFirstAsync`, `TryRewriteAsync`, `TryHideAsync`,
`TryRestoreAsync`, `TryHideAnswerAsync` and `TryRestoreAnswerAsync`. Each is one `ExecuteUpdateAsync` whose `WHERE`
carries the state it moves from. The repository's private `GuardedAsync` opens a transaction **inside the
execution strategy** (Catalog retries on failure), runs the statement, and only if it changed exactly one row runs
the caller's `stage` callback (the audit entry and the notice) and saves - so the outbox rows commit with the
change or not at all. This is the shape `ReviewRepository.GuardedAsync` already had (specs/046).

**Rationale**: Principle III: a decision and what announces it commit together. A guarded statement is also what
makes two people at once safe: of two moderators hiding the same question, one statement matches and the other
matches nothing and becomes a 409.

**Alternatives considered**:

- **Load the row, change it in memory, save** with EF change tracking. Not recorded as considered; it would need an
  optimistic concurrency token to make two moderators at once safe, which the guarded statement makes unnecessary.
- **Open the transaction outside the execution strategy.** Not recorded as considered here. The repository's own
  comment gives the reason it is inside: Catalog retries on failure, and CLAUDE.md records (from the vouchers
  feature, specs/069, in Order) that a hand-opened transaction outside a retrying strategy throws.

---

## D6 - The asker is told once, on the first answer; two first answers at once become one answer and one rewrite

**Decision**: Answering first tries `TryAnswerFirstAsync` (`WHERE "AnsweredAt" IS NULL AND "HiddenAt" IS NULL`),
whose `stage` records `QuestionAnswered` and sends `QuestionAnswered` to the asker. If that changes nothing, it
tries `TryRewriteAsync` (`WHERE "AnsweredAt" IS NOT NULL AND "AnswerHiddenAt" IS NULL AND "HiddenAt" IS NULL`),
whose `stage` records `AnswerEdited` and tells nobody. If that too changes nothing, the question or its answer is
hidden and the answer is a 409 (D3).

`AnsweredAt` is the first answer's time and never moves; a rewrite moves `AnswerUpdatedAt`. The response's
`answerEdited` is true when `AnswerUpdatedAt` is more than one second after `AnsweredAt`.

**Rationale**: The first answer is what the asker was waiting for; an edit is not news. Because the first answer is
decided by the database's guard, ten simultaneous first answers produce exactly one notice
(`Ten_first_answers_at_once_tell_the_asker_once`), and the PR's mutation "removing the first-answer guard" turned
three tests red, that one among them.

**Alternatives considered**:

- **Notify on every edit.** Rejected in the spec ("once, on the first answer, not on every edit"); the reason given
  is only that; no further reasoning is recorded.
- **Answer the second concurrent first answer with a 409.** Not recorded as considered. Turning it into a rewrite
  means the seller's last click wins, as it does for any rewrite.

---

## D7 - Only a product on sale can be asked about, and a seller does not ask about their own

**Decision**: `AskQuestionCommand` loads the product and answers `404 Product not found.` when it is missing, not
listed (`!IsListed`, specs/045) or inactive - the public lookup's rule. A caller who is the product's seller gets
**403** `You cannot ask about your own product.`, through `ForbiddenException`, whose message is shown.

**Rationale**: Asking about something nobody can buy is noise, and not-on-sale must not be distinguishable from
not-there (specs/045). The code's own comment on the seller rule: "A seller asking their own shop something is a
note to themselves on a public page." A 403 does not leak anything here, because the product is already public.

**Alternatives considered**: not recorded.

---

## D8 - The asker is signed from the token

**Decision**: `AskerName` is the token's `given_name` (`ICurrentUser.GivenName`), as a review's is (specs/046); a
token from before it carried one falls back to the email's first letter and a full stop, and to `?` with neither.
`AskerId` and `AnsweredBy` are `ICurrentUser.Id`. The request bodies carry only the words: `{ body }`,
`{ answer }`, `{ reason }`.

**Rationale**: Principle IV. A name in the request would let anybody sign as anybody.

**Alternatives considered**:

- **A display name in the request body.** Rejected by Principle IV; not otherwise discussed.

---

## D9 - The shop's own questions notify nobody in particular

**Decision**: `NewQuestion` goes to the product's seller. A question on the shop's own product sends no notice;
staff find it on the *To answer* tab of `/admin/questions`, backed by `GET /api/questions/to-answer`.

**Rationale**: There is no one person who is "the shop" (the plan: "The shop's own products have no one person to
tell: staff see them in their queue").

**Alternatives considered**:

- **Notify every staff member.** Not recorded as considered.

---

## D10 - One table in Catalog, each part hidden on its own

**Decision**: `product_questions` in `ecommerce_catalog_db`, one row per question holding its one answer (D1), with
separate hidden columns for the question (`HiddenAt`, `HiddenReason`, `HiddenBy`) and for the answer
(`AnswerHiddenAt`, `AnswerHiddenReason`, `AnswerHiddenBy`). Hidden, never deleted. `ProductId` cascades from
`products`, so deleting a product deletes its questions.

**Rationale**: Catalog owns the product and already owns reviews; the question needs the product's seller and its
listing state, both on Catalog's rows (Principle I). One answer per question makes a second table unnecessary. The
migration only adds a table, so the previously released image still runs against it.

**Alternatives considered**:

- **An `answers` table.** Not recorded as considered; D1 makes it unnecessary.
- **Deleting instead of hiding.** Not recorded as considered. The feature follows reviews (specs/046, specs/059),
  which are hidden, not deleted, and which staff can restore.

---

## D11 - The storefront draws the answer box; the server decides

**Decision**: `components/product/product-questions` draws the answer box from `product.sellerId` and the caller's
roles (`mayAnswer = product.sellerId ? ownProduct : isStaff`), and the ask form for a signed-in customer who is not
the seller. It is for drawing only: a refusal from the server is shown in the server's words.

**Rationale**: CLAUDE.md's rule since specs/028: a role on the response is for drawing, never for deciding.

**Alternatives considered**: not recorded.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| No rate limit of its own | A signed-in customer can post many questions | The gateway's limits only (out of scope in the spec); staff hide |
| The shop's own questions notify nobody | They wait until staff open the queue | Stated in Known limits of the docs page |
| The public list read the product page as its gate (D4) | Questions on an unlisted product were readable by address | Fixed by specs/081 (#169) |
