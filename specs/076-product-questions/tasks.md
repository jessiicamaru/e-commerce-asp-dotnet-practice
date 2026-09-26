---
description: "Task list for product questions"
---

# Tasks: A shopper asks a seller about a product

> Completed on 2026-09-27, after the feature merged (#160), from the code at that merge, the pull request and docs/features/product-questions.md.

**Input**: Design documents from `/specs/076-product-questions/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included and written first (T001). The property the issue accepts on - nobody but the product's seller
answers - is an authorization boundary, and one notice among concurrent first answers is a database property; both
need an automated check against a real PostgreSQL (constitution Principle V).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 ask, US2 answer, US3 moderate

## As recorded at the merge

The five tasks the feature was built from, kept as written:

- [X] T001 Tests first, in `ProductQuestionTests`:
  - asking needs a product on sale, and a seller does not ask about their own;
  - the list is public, newest first, without hidden questions and with a hidden answer blanked;
  - only the seller answers a seller's product, and an administrator on it is a 404;
  - staff answer the shop's own;
  - the first answer tells the asker and a rewrite does not;
  - concurrent first answers produce one notice;
  - hiding and restoring, with who is told;
  - a hidden answer cannot be rewritten;
  - the queues.
- [X] T002 The entity, configuration, migration, repository, features, controller and gateway route.
- [X] T003 The notification kinds: the declaration and the `Notifier` constants.
- [X] T004 Storefront: the service, hooks, product-page section, the seller and admin queues, routes, menu links
  and words. Vitest.
- [X] T005 Bruno, the reference, mutation checks, and the docs.

The same work at file level follows, reconstructed on 2026-09-27 from the files #160 changed. Each task below is
part of one of T001 to T005, named in brackets at the end.

---

## Phase 1: Foundational (blocks every story)

- [X] T006 [P] Create `ProductQuestion` in `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/ProductQuestion.cs`: the question, its hidden columns, the one answer with `AnsweredAt` (first) and `AnswerUpdatedAt` (last), and the answer's hidden columns; `Id = Guid.CreateVersion7()` (T002)
- [X] T007 [P] Map it in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/ProductQuestionConfiguration.cs`: table `product_questions`, `ValueGeneratedNever()`, lengths 100/1000/500/2000/500, index `(ProductId, CreatedAt)`, partial index on `CreatedAt WHERE "AnsweredAt" IS NULL AND "HiddenAt" IS NULL`, FK to `products` with cascade (T002)
- [X] T008 Add `DbSet<ProductQuestion> ProductQuestions` to `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/CatalogDbContext.cs` and generate `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/20260926101826_AddProductQuestions.cs` (a table only - additive) (T002)
- [X] T009 Declare `IProductQuestionRepository` and `QuestionRow` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductQuestionRepository.cs`: three reads, `AddAsync` / `SaveChangesAsync`, and six guarded `Try...Async` methods taking a `stage` callback (T002)
- [X] T010 Implement `ProductQuestionRepository` in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductQuestionRepository.cs`: each transition one `ExecuteUpdateAsync` guarded on the state it leaves, run by a private `GuardedAsync` that stages and saves only when one row changed, in a transaction inside the execution strategy (T002)
- [X] T011 Register the repository in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/DependencyInjection.cs` and in `server/tests/Ecommerce.Catalog.Tests/CatalogTestFixture.cs` (T002)
- [X] T012 [P] Add `NewQuestion`, `QuestionAnswered`, `QuestionHidden`, `AnswerHidden` to `NotificationKind` in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs` and declare their `required` data keys in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json` (T003)
- [X] T013 [P] Add `catalog-questions-route` (`/api/questions/{**catch-all}`) and `catalog-questions-root-route` (`/api/questions`) to `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json` (T002)

**Checkpoint**: the solution builds, the migration applies, and the gateway forwards `/api/questions` to Catalog.

---

## Phase 2: User Story 1 - Ask (P1)

**Goal**: A customer asks about a product on sale; the seller is told.

- [X] T014 [P] [US1] Test `Asking_needs_a_product_on_sale_and_a_seller_does_not_ask_about_their_own` and `Asking_tells_the_seller_and_signs_it_with_the_askers_first_name` in `server/tests/Ecommerce.Catalog.Tests/ProductQuestionTests.cs` (T001)
- [X] T015 [US1] Implement `AskQuestionCommand`, its validator and its handler in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Questions/QuestionFeatures.cs`: the public lookup's 404 (missing, `!IsListed`, inactive), 403 `You cannot ask about your own product.`, `AskerName` from `given_name`, then insert, audit `QuestionAsked`, notice `NewQuestion` to the seller, one save (T002)
- [X] T016 [US1] Implement `GetProductQuestionsQuery` with its paging validator and `QuestionResponse.Public` (hidden questions left out, a hidden answer blanked, no reasons) in `.../Application/Questions/QuestionFeatures.cs` (T002)
- [X] T017 [US1] Add `GET` (`[AllowAnonymous]`) and `POST` (`Customer`) `products/{productId:guid}/questions` to `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/QuestionsController.cs` (T002)

**Checkpoint**: quickstart scenarios 2 and 4.

---

## Phase 3: User Story 2 - Answer (P1)

**Goal**: Only the product's seller answers - staff for the shop's own - and the asker is told once.

- [X] T018 [P] [US2] Test `Only_the_products_seller_answers_it_and_an_administrator_there_is_a_404`, `Staff_answer_the_shops_own_and_a_seller_cannot`, `A_rewrite_replaces_the_answer_and_tells_nobody_again`, `Ten_first_answers_at_once_tell_the_asker_once` and `A_sellers_queue_holds_their_own_unanswered_and_staffs_the_shops_own` in `server/tests/Ecommerce.Catalog.Tests/ProductQuestionTests.cs` (T001)
- [X] T019 [US2] Implement `AnswerableAsync` in `QuestionHandlers` (`.../Application/Questions/QuestionFeatures.cs`): a seller's product, only that seller; the shop's own, only staff; anybody else the one `404 Question not found.` (research D2) (T002)
- [X] T020 [US2] Implement `AnswerQuestionCommand`: `TryAnswerFirstAsync` staging audit `QuestionAnswered` and notice `QuestionAnswered`, falling back to `TryRewriteAsync` staging `AnswerEdited`, then 409 `This question or its answer was hidden by a moderator.` (research D3, D6) (T002)
- [X] T021 [US2] Implement `GetQuestionsToAnswerQuery` (the caller's own products, or for staff the shop's own) with `QuestionResponse.Full` (T002)
- [X] T022 [US2] Add `PUT questions/{id:guid}/answer` and `GET questions/to-answer`, both `[Authorize(Roles = "Seller,Admin,Moderator")]` - wide at the door so the handler's rule is reachable - to `.../WebApi/Controllers/QuestionsController.cs` (T002)

**Checkpoint**: quickstart scenarios 3 and 5; SC-001 and SC-002.

---

## Phase 4: User Story 3 - Moderate (P2)

**Goal**: Staff hide or restore a question or only its answer, with a reason; a hidden answer is locked.

- [X] T023 [P] [US3] Test `The_list_is_public_newest_first_and_a_hidden_question_leaves_it_until_restored`, `A_hidden_answer_reads_as_unanswered_and_cannot_be_rewritten_until_restored` and `A_hidden_question_cannot_be_answered` in `server/tests/Ecommerce.Catalog.Tests/ProductQuestionTests.cs` (T001)
- [X] T024 [US3] Implement `HideQuestionCommand`, `RestoreQuestionCommand`, `HideAnswerCommand`, `RestoreAnswerCommand` and the two reason validators in `.../Application/Questions/QuestionFeatures.cs`: each one guarded statement staging its Moderation audit entry, hides also notifying the asker (`QuestionHidden`) or the answerer (`AnswerHidden`); 409 with its own message when nothing matched (T002)
- [X] T025 [US3] Implement `GetQuestionsForStaffQuery` (visible, or anything hidden) (T002)
- [X] T026 [US3] Add `GET questions` and `POST questions/{id}/hide`, `/restore`, `/answer/hide`, `/answer/restore`, all `[Authorize(Roles = StaffRoles.Staff)]`, to `.../WebApi/Controllers/QuestionsController.cs` (T002)

**Checkpoint**: quickstart scenarios 6 and 7; SC-003 and SC-004.

---

## Phase 5: Storefront

- [X] T027 [P] Create the `Questions` service class in `client/src/services/question/index.ts` and its `Question` type in `client/src/services/question/types.ts`, with `client/src/services/question/index.test.ts` (every address, no user or seller id) (T004)
- [X] T028 [P] Add the query keys to `client/src/constants/query-keys/index.ts` and the hooks in `client/src/hooks/question/index.ts` (every change refreshes every question list and the moderator's decisions) (T004)
- [X] T029 [US1] [US2] Create `client/src/components/product/product-questions/index.tsx` and its test `index.test.tsx` - the list, the ask form for a customer who is not the seller, an answer box drawn from `product.sellerId` and the roles - and place it under the reviews in `client/src/pages/product/index.tsx` (T004)
- [X] T030 [P] [US2] Create `client/src/components/question/question-item/index.tsx`, `client/src/components/question/answer-form/index.tsx` and `client/src/components/question/answer-queue/index.tsx` (T004)
- [X] T031 [US2] Create `client/src/pages/shop-questions/index.tsx` with `index.test.tsx`, route `/shop/questions` in `client/src/routes/index.tsx`, and its menu link in `client/src/layouts/seller-layout/index.tsx` (T004)
- [X] T032 [US3] Create `client/src/pages/admin-questions/index.tsx`, `moderation-actions.tsx` and `index.test.tsx` (tabs *To answer*, *Visible*, *Hidden*; hiding asks for a reason through `TextPrompt`), `client/src/components/shared/tab-strip/index.tsx`, route `/admin/questions`, and its menu link in `client/src/layouts/admin-layout/index.tsx` (T004)
- [X] T033 [P] Add the words to `client/src/locales/{vi,en}/catalog.json` (`questions.*`), `seller.json`, `admin.json` and `notifications.json` (the four kinds) (T004)

---

## Phase 6: Polish and evidence

- [X] T034 [P] Add Bruno requests `bruno/seller/` seq 67 to 77: approve the product again, ask, the public list, the seller's queue, an administrator answering for a seller (404), the seller answers, a seller asking about their own product (403), staff hide the answer, the rewrite refused (409), a customer on the staff list (403), asking without a token (401) (T005)
- [X] T035 Run the seven mutation checks recorded in the PR - staff answering a seller's product, an unlisted product asked about, a hidden answer rewritten, the first-answer guard removed, hidden questions shown, a hidden answer shown, a seller asking about their own - each turning `ProductQuestionTests` red (T005)
- [X] T036 [P] Write `docs/features/product-questions.md`, link it from `docs/README.md`, and update `docs/overview/project-overview.md`, `docs/testing/testing-strategy.md`, `docs/project/timeline.md`, `docs/project/backlog.md` (#110 to Fixed) and `CLAUDE.md` (T005)
- [X] T037 Regenerate `docs/reference/api.md`, `docs/reference/data-model.md` and `docs/reference/gateway.md` with `python docs/tools/generate_reference.py` (9 endpoints, 1 table, 2 gateway routes) (T005)
- [X] T038 Run `Ecommerce.Catalog.Tests` (186/186), the storefront suite (427/427 in 74 files, lint and `tsc -b` clean) and the Bruno collection through the rebuilt storefront container (249/249 requests, 403/403 tests), and merge through PR #160, closing #110 (T005)

---

## Dependencies & Execution Order

- **Phase 1** blocks everything: the table, the repository and the kinds are shared by all three stories.
- **US1** before **US2** before **US3**: an answer needs a question, and hiding an answer needs an answer. Each
  story's tests (T014, T018, T023) come before its implementation.
- **The storefront** (Phase 5) needs the endpoints of the stories it draws.
- **Polish** last: the mutations need the tests, the reference needs the endpoints.

## Notes

- 38 tasks: the 5 recorded at the merge, and 33 reconstructed from them - 8 foundational, 4 for US1, 5 for US2,
  4 for US3, 7 storefront, 5 polish.
- 10 server tests in one file, `ProductQuestionTests`, rather than a file per story.
- Later change, not part of this feature: specs/081 (#169) added a product check to `GetProductQuestionsQuery`; see
  [research.md D4](./research.md).
