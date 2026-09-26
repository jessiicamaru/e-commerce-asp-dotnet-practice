# Product questions

A shopper can ask about a product on its page, for example "does this come with the charger?", and the product's
**seller** answers in public (specs/076, #110). For the shop's own products, **staff** answer. Staff can also hide a
question, or only its answer, the way they hide reviews.

Reviews (specs/046) are opinions from people who received the product. Questions are for anybody deciding whether
to buy it.

## What people can do

| Role | Capabilities |
| :-- | :-- |
| Anyone | Reads a product's questions and answers. |
| Customer | Asks about a product that is on sale. |
| Seller | Answers questions about their own products, and rewrites an answer. **Questions** in the shop console lists what waits for them and what they answered. They cannot ask about their own product. |
| Staff (Admin, Moderator) | Answer questions about the shop's own products from **Questions** in the admin console. Hide or show again any question, or only its answer, with a reason. |

## Rules and guarantees

1. **Only the product's seller answers as the seller.** For the shop's own products (`SellerId` null), only staff
   answer.
   - Anybody else gets `404 Question not found.`, the same answer as a question that does not exist.
   - That includes an **administrator on a seller's product**.
   - *Why:* `SellerOwnership` lets an administrator through every product write, because moderation is the job.
     An answer, though, is published as the seller's words. Staff moderate everyone's answers and speak only for
     the shop.
2. **One answer per question.** A rewrite replaces it and marks it edited. This is not a thread, so shoppers do
   not answer each other.
3. **The asker is told once, on the first answer.** Two first answers at once end up as one first answer and
   one rewrite.
   - The first answer is `UPDATE ... WHERE "AnsweredAt" IS NULL`.
   - The rewrite is `... WHERE "AnsweredAt" IS NOT NULL AND "AnswerHiddenAt" IS NULL`.
4. **Only a product on sale can be asked about.** Anything else is the public lookup's 404 (specs/045). A seller
   asking about their own product gets a 403 in words.
5. **Hidden, never deleted.**
   - A hidden **question** leaves the product page together with its answer, and its asker is told why.
   - A hidden **answer** leaves the question reading as unanswered, and whoever answered is told why.
   - The answer is **locked until staff show it again** (a 409). Otherwise hiding would last only until the
     seller's next edit.
6. **Every change is one guarded statement.** Its audit entry and its notice are staged in that statement's
   transaction, inside the execution strategy. Of two moderators at once, one hides and the other gets a 409.
7. **Signed from the token.** The asker's first name is the token's `given_name`, as a review's is. The request
   never names who asks or who answers.

## Data

`product_questions`, in `ecommerce_catalog_db`, holds one row per question with its one answer:

| Columns | Notes |
| :-- | :-- |
| `Id`, `ProductId`, `AskerId`, `AskerName`, `Body` (1000), `CreatedAt` | The question. `ProductId` cascades from `products`. |
| `HiddenAt`, `HiddenReason`, `HiddenBy` | The question hidden. |
| `Answer` (2000), `AnsweredBy`, `AnsweredAt`, `AnswerUpdatedAt` | The answer. `AnsweredAt` is the first answer; a rewrite moves `AnswerUpdatedAt`. |
| `AnswerHiddenAt`, `AnswerHiddenReason`, `AnswerHiddenBy` | The answer hidden. |

The indexes:
- `(ProductId, CreatedAt)` reads a product's page.
- A partial index on `CreatedAt WHERE "AnsweredAt" IS NULL AND "HiddenAt" IS NULL` reads the unanswered queues.

Migration `AddProductQuestions` only adds, so an earlier image still runs against it.

## API

| Method | Path | Who | What |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/products/{id}/questions` | Anonymous | The visible questions, newest first. A hidden answer comes back as no answer, and no reasons are included. |
| `POST` | `/api/products/{id}/questions` | Customer | Ask: `{ body }`. |
| `PUT` | `/api/questions/{id}/answer` | Seller, Staff | Answer or rewrite: `{ answer }`. Not theirs is 404; hidden is 409. |
| `GET` | `/api/questions/to-answer?answered=` | Seller, Staff | A seller's own products, or for staff the shop's own. Unanswered comes oldest first, answered newest first. |
| `GET` | `/api/questions?hidden=` | Staff | Every question: the visible ones, or those with something hidden. The reasons are included. |
| `POST` | `/api/questions/{id}/hide`, `/restore` | Staff | The question. Hiding takes `{ reason }`. |
| `POST` | `/api/questions/{id}/answer/hide`, `/answer/restore` | Staff | The answer alone. Hiding takes `{ reason }`. |

The gateway routes are `catalog-questions-route` and `catalog-questions-root-route`. The notification kinds are
declared in `notification-kinds.json` and worded in both languages:

| Kind | Data | Sent to |
| :-- | :-- | :-- |
| `NewQuestion` | `{product}` | The seller |
| `QuestionAnswered` | `{product}` | The asker |
| `QuestionHidden` | `{product, reason}` | The asker |
| `AnswerHidden` | `{product, reason}` | Whoever answered |

## Storefront

| Path | What it does |
| :-- | :-- |
| `client/src/components/product/product-questions/` | The **Questions** section under the reviews: the list, the ask form for a customer who is not the seller, and an answer box for whoever answers this product. The box is drawn from `product.sellerId` and the roles, and is for drawing only. |
| `client/src/components/question/question-item/` | One question and its answer, with the shop's name. The product page and both queues use it. |
| `client/src/components/question/answer-form/` | Answer or rewrite. It is folded to a button until opened, and a refusal is shown in the server's words. |
| `client/src/components/question/answer-queue/` | The caller's queue, unanswered or answered. |
| `client/src/pages/shop-questions/` | `/shop/questions`, linked in the seller menu. |
| `client/src/pages/admin-questions/` | `/admin/questions`, with the tabs *To answer*, *Visible* and *Hidden*. `moderation-actions.tsx` beside it hides with a reason through `TextPrompt`. |
| `client/src/components/shared/tab-strip/` | The pill tabs both pages use. |

## Tests

| Where | What it proves |
| :-- | :-- |
| `Ecommerce.Catalog.Tests/ProductQuestionTests` (10) | Asking needs a product on sale, and a seller does not ask about their own. The seller is told and the question is signed with the first name. Only the product's seller answers, and another seller, an administrator and a made-up id are the same 404. Staff answer the shop's own. A rewrite tells nobody. Ten first answers at once tell the asker once. The public list is newest first, and a hidden question leaves it until restored, with its asker told. A hidden answer reads as unanswered and is locked until restored. A hidden question cannot be answered. The queues. |
| `client/src/components/product/product-questions/index.test.tsx` | The answer is named after the shop. A customer asks with only the words. The product's seller gets an answer box and no ask form, and another seller does not. Staff answer only the shop's own. A refusal is shown in the server's words. |
| `client/src/pages/shop-questions/index.test.tsx`, `pages/admin-questions/index.test.tsx` | The queues ask for the right list. Hiding an answer asks why. The reasons are shown and both parts can be put back. |
| `client/src/services/question/index.test.ts` | Every address, with no user or seller id. |
| `bruno/seller/` 67-77 | Ask, the public list, the seller's queue, an administrator answering for a seller (404), the seller answering, a seller asking about their own product (403), staff hiding the answer, the rewrite refused (409), a customer on the staff list (403), and asking without a token (401). |

Mutation checks (specs/076): each of these turns `ProductQuestionTests` red.
- Letting staff answer a seller's product.
- Letting an unlisted product be asked about.
- Letting a hidden answer be rewritten.
- Removing the first-answer guard. Three tests fail, including the ten-at-once test.
- Showing hidden questions in the public list.
- Showing a hidden answer.
- Letting a seller ask about their own product.

## Known limits

- **One answer, the seller's.** Shoppers cannot answer each other, and nobody can vote an answer helpful.
- **An asker cannot edit or delete their question.** Staff can hide it.
- **The shop's own questions notify nobody in particular.** Staff read the queue.
- **No rate limit beyond the gateway's.**
