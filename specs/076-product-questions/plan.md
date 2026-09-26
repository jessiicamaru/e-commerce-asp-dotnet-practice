# Implementation Plan: A shopper asks a seller about a product

**Branch**: `076-product-questions` | **Spec**: [spec.md](spec.md) | **Issue**: #110

## Design (Catalog)

- `ProductQuestion` (Domain) holds the question and its one answer, each with its own hidden columns. The
  migration is `AddProductQuestions`: a table only, so an earlier image still runs against it.
- `IProductQuestionRepository` does the reads, plus `TryAskAsync`, `TryAnswerFirstAsync`, `TryRewriteAsync`,
  `TryHideAsync`, `TryRestoreAsync`, `TryHideAnswerAsync` and `TryRestoreAnswerAsync`.
  - Each is one guarded statement, and only if it changed a row does `stage` run (the audit entry and the notice).
    The save happens in the same transaction, inside the execution strategy, as `ReviewRepository.GuardedAsync`
    does.
- `Application/Questions/QuestionFeatures.cs` holds the commands, the queries, the validators and one handler
  class.
  - **Who answers**, `QuestionAnswerers.MayAnswer(product, caller)`:
    - a product with a seller: only that seller;
    - the shop's own (`SellerId` null): only staff;
    - anyone else: 404 `Question not found.`, one message for "not yours" and "not there".
  - **Answering** tries the first answer. If that changes nothing, it tries the rewrite. If that also changes
    nothing, the question is hidden, or its answer is (409).
  - **The public list** leaves out hidden questions and blanks a hidden answer. The staff views carry the reasons.
- `QuestionsController`, `[Route("api")]`. The routes are `products/{productId:guid}/questions` and
  `questions/...`, and the gateway needs a `questions` route to Catalog.

## Storefront

- `services/question` (the `Questions` class and its types) and `hooks/question`.
- `components/product/product-questions` goes on the product page, under the reviews: the list, an ask form for a
  signed-in customer who is not the seller, and an answer box on each question for whoever may answer.
  - The box is drawn from `product.sellerId` and the caller's roles. It is for drawing only: the server decides.
- `components/question/question-item` is the question and its answer, shared by the product page and the queues.
- `pages/shop-questions` (`/shop/questions`, the seller layout) and `pages/admin-questions` (`/admin/questions`,
  the admin layout) are the queues. Hiding a question or an answer uses `components/shared/text-prompt` for the
  reason.
- The words go in `catalog` (`questions.*`), `seller`, `admin` and `notifications` (the four kinds), in `vi` and
  `en`.

## Research

- **D1 - one answer, the seller's.** A thread invites shoppers to answer each other, which is a second moderation
  surface and not what the issue asks for. The answer is written as the shop, so a rewrite replaces it.
- **D2 - an administrator does not answer on a seller's product.** `SellerOwnership` lets an administrator
  through every write, because moderation is the job. An answer, though, is published as the seller's words, so
  staff answer only for the shop's own products. They moderate everyone's.
- **D3 - a hidden answer is locked until restored.** Otherwise a seller undoes the moderator with an edit.
- **D4 - a question on a product that is no longer on sale.** It stays readable by the public list (the product
  page is itself a 404 for the public), and the seller can still answer it.
