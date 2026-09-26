# Implementation Plan: A shopper asks a seller about a product

> Completed on 2026-09-27, after the feature merged (#160), from the code at that merge, the pull request and docs/features/product-questions.md.

**Branch**: `076-product-questions` | **Spec**: [spec.md](spec.md) | **Issue**: #110

**Merged**: #160, 2026-09-26 | **Research**: [research.md](research.md) | **Data model**: [data-model.md](data-model.md)

## Summary

*Added on 2026-09-27.* A customer asks about a product on sale; the product's **seller** answers in public, once,
and may rewrite the answer; for the shop's own products **staff** answer; anybody else - an administrator on a
seller's product included - gets the one `404 Question not found.`. Staff hide a question or only its answer, with
a reason, and a hidden answer is locked until restored. Everything lives in Catalog, in one new table,
`product_questions`; every change after the insert is one guarded statement whose audit entry and notice are
staged in its own transaction. The storefront gets a Questions section on the product page, `/shop/questions` and
`/admin/questions`.

The sections **Design (Catalog)**, **Storefront** and **Research** below are the plan as written before the build,
kept word for word. The research decisions are also in [research.md](research.md) as D1 to D4, with their
alternatives, and D5 to D11 record what the code decided without a heading.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (server); TypeScript, React 19, Vite (storefront)

**Primary Dependencies**: MediatR 12.4.1, FluentValidation 12.1.1, EF Core with Npgsql 10.0.3, MassTransit 8.3.6
(Catalog's existing EF outbox), `Ecommerce.Shared` (`IAuditTrail`, `INotifier`, `ICurrentUser`, `StaffRoles`,
`GlobalExceptionHandler`), `Ecommerce.Contracts/Activity` (unchanged); storefront: axios, TanStack Query,
react-i18next, shadcn/ui

**Storage**: PostgreSQL 16, `ecommerce_catalog_db` (host port 5433) - one new table

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Catalog.Tests`, `ProductQuestionTests`, 10 tests), with
MassTransit's test harness observing the published notices; Vitest + Testing Library in `client/`; Bruno through
the gateway

**Target Platform**: Catalog on 5057 behind the YARP gateway on 5000; the storefront through Vite's proxy or its
nginx image

**Project Type**: An addition to an existing Clean Architecture microservice, plus storefront pages

**Performance Goals**: None stated. The two indexes serve the product page (`ProductId, CreatedAt`) and the
unanswered queues (a partial index); no measurement was recorded

**Constraints**: Only the product's seller answers (the issue's acceptance); one notice on the first answer under
concurrency; a hidden answer cannot be rewritten; the migration must be additive so an earlier Catalog image still
runs

**Scale/Scope**: 9 endpoints, 1 table, 2 gateway routes, 4 notification kinds (the PR's own count)

## Constitution Check

*Evaluated after the merge against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. The plan as
written before the build had no Constitution Check; this section was added on 2026-09-27 from the code.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Everything is in Catalog, which owns the product, its seller id and its listing state - the facts the ask and answer rules read. No other database is read; no call to another service is added. The notices and audit entries leave through the existing `Ecommerce.Contracts/Activity` records, which the Activity service owns; no contract record changed |
| **II. Clean Architecture Layering** | **Pass.** `ProductQuestion` in Domain has no dependencies; the commands, queries, validators and handler, and `IProductQuestionRepository`, are in Application; `ProductQuestionRepository` and the configuration in Infrastructure, registered in its `DependencyInjection.cs`; `QuestionsController` only sends through MediatR. One deviation from the folder convention: the use cases share one file, `Application/Questions/QuestionFeatures.cs`, rather than a folder per command - the same shape reviews already had (`Reviews/ReviewFeatures.cs`). It is a layout choice, not a dependency leak |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Asking stages the row, records the audit entry and the notice, then saves once. Every other change is a guarded `UPDATE` whose `WHERE` carries the state it leaves; only when it changed one row are the audit entry and notice staged and saved, in the same transaction inside the execution strategy (research D5). A repeated decision affects zero rows and is a 409; ten first answers at once produce one notice. The feature consumes no message |
| **IV. Identity Comes From the Token** | **Pass.** `AskerId`, `AskerName` (`given_name`), `AnsweredBy` and `HiddenBy` all come from `ICurrentUser`; the request bodies carry only words. Reading is `[AllowAnonymous]` explicitly; every write names its roles. Who may answer is decided from the product's row against the token, not from anything sent - and the controller attribute is deliberately wide (`Seller,Admin,Moderator`) so that the handler's rule is reachable (the specs/027 lesson) |
| **V. Evidence Over Assumption** | **Pass.** `ProductQuestionTests` runs against a real PostgreSQL, because the property under test - one first answer among ten concurrent ones - is the database's row locking on a guarded statement. The PR records seven mutations, each turning the suite red; Bruno ran through the rebuilt storefront container (249/249 requests). What was not verified is said: no load or latency figure was measured |

**Post-design re-check** (against the code at the merge): no violations. The one-file layout of the Application
layer is noted under Principle II and needed no Complexity Tracking entry because no dependency points the wrong
way.

## Design (Catalog)

- `ProductQuestion` (Domain) holds the question and its one answer, each with its own hidden columns. The
  migration is `AddProductQuestions`: a table only, so an earlier image still runs against it.
- `IProductQuestionRepository` does the reads, plus `TryAskAsync`, `TryAnswerFirstAsync`, `TryRewriteAsync`,
  `TryHideAsync`, `TryRestoreAsync`, `TryHideAnswerAsync` and `TryRestoreAnswerAsync`.
  - Each is one guarded statement, and only if it changed a row does `stage` run (the audit entry and the notice).
    The save happens in the same transaction, inside the execution strategy, as `ReviewRepository.GuardedAsync`
    does.
  - *Corrected on 2026-09-27: the code has no `TryAskAsync`.* Asking is a plain insert - `AddAsync`, then the audit
    entry and the notice, then one `SaveChangesAsync` - because a new row has no earlier state to guard against.
    The six `Try...` methods above are the guarded ones.
- `Application/Questions/QuestionFeatures.cs` holds the commands, the queries, the validators and one handler
  class.
  - **Who answers**, `QuestionAnswerers.MayAnswer(product, caller)`:
    - a product with a seller: only that seller;
    - the shop's own (`SellerId` null): only staff;
    - anyone else: 404 `Question not found.`, one message for "not yours" and "not there".
  - *Corrected on 2026-09-27: there is no `QuestionAnswerers` class.* The rule is the private method
    `AnswerableAsync` in `QuestionHandlers` (`product.SellerId is { } seller ? seller == me : IsStaff()`), with the
    same three outcomes.
  - **Answering** tries the first answer. If that changes nothing, it tries the rewrite. If that also changes
    nothing, the question is hidden, or its answer is (409).
  - **The public list** leaves out hidden questions and blanks a hidden answer. The staff views carry the reasons.
- `QuestionsController`, `[Route("api")]`. The routes are `products/{productId:guid}/questions` and
  `questions/...`, and the gateway needs a `questions` route to Catalog.
  - *Completed on 2026-09-27:* two gateway routes, `catalog-questions-route` (`/api/questions/{**catch-all}`) and
    `catalog-questions-root-route` (`/api/questions`), both to `catalog-cluster`.

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
- *Completed on 2026-09-27 from the PR:* two more components, `components/question/answer-form` (folded to a
  button until opened) and `components/question/answer-queue` (the caller's queue, shared by both pages), and a
  new `components/shared/tab-strip`. The admin page's moderation buttons are in `pages/admin-questions/moderation-actions.tsx`.

## Research

- **D1 - one answer, the seller's.** A thread invites shoppers to answer each other, which is a second moderation
  surface and not what the issue asks for. The answer is written as the shop, so a rewrite replaces it.
- **D2 - an administrator does not answer on a seller's product.** `SellerOwnership` lets an administrator
  through every write, because moderation is the job. An answer, though, is published as the seller's words, so
  staff answer only for the shop's own products. They moderate everyone's.
- **D3 - a hidden answer is locked until restored.** Otherwise a seller undoes the moderator with an edit.
- **D4 - a question on a product that is no longer on sale.** It stays readable by the public list (the product
  page is itself a 404 for the public), and the seller can still answer it.
  - *Note added on 2026-09-27:* the first half was superseded on 2026-09-26 by specs/081 (#169, issue #166) - the
    public list is now a 404 for anybody but the product's seller and staff while it is off the shelf. The second
    half still holds. See [research.md D4](research.md).

## Project Structure

### Documentation (this feature)

```text
specs/076-product-questions/
├── spec.md                  # User stories, FR-001..FR-014, SC-001..SC-005
├── plan.md                  # This file
├── research.md              # D1..D11
├── data-model.md            # product_questions, its transitions and migration
├── quickstart.md            # Validation scenarios
├── contracts/
│   ├── http-api.md          # The nine endpoints
│   └── messages.md          # The audit entries and the four notification kinds
├── checklists/
│   └── requirements.md      # Spec quality checklist
└── tasks.md                 # T001..T005 as built, broken down in T006..
```

### Source code touched (from #160)

```text
server/src/Services/Catalog/
├── Ecommerce.Catalog.Domain/Entities/ProductQuestion.cs                              # new
├── Ecommerce.Catalog.Application/
│   ├── Common/Interfaces/IProductQuestionRepository.cs                               # new, with QuestionRow
│   └── Questions/QuestionFeatures.cs                                                 # new: records, validators, QuestionHandlers
├── Ecommerce.Catalog.Infrastructure/
│   ├── Configurations/ProductQuestionConfiguration.cs                                # new
│   ├── Migrations/20260926101826_AddProductQuestions.cs (+ Designer, snapshot)       # new
│   ├── Persistence/CatalogDbContext.cs                                               # DbSet
│   ├── Persistence/Repositories/ProductQuestionRepository.cs                         # new, GuardedAsync
│   └── DependencyInjection.cs                                                        # registration
└── Ecommerce.Catalog.WebApi/Controllers/QuestionsController.cs                       # new

server/src/BuildingBlocks/Ecommerce.Shared/Notifications/
├── Notifier.cs                          # four NotificationKind constants
└── notification-kinds.json              # four kinds declared

server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json                           # two routes
server/tests/Ecommerce.Catalog.Tests/ProductQuestionTests.cs                          # new, 10 tests
server/tests/Ecommerce.Catalog.Tests/CatalogTestFixture.cs                            # registration

client/src/
├── services/question/{index.ts,types.ts,index.test.ts}
├── hooks/question/index.ts
├── components/product/product-questions/{index.tsx,index.test.tsx}
├── components/question/{question-item,answer-form,answer-queue}/index.tsx
├── components/shared/tab-strip/index.tsx
├── pages/shop-questions/{index.tsx,index.test.tsx}
├── pages/admin-questions/{index.tsx,index.test.tsx,moderation-actions.tsx}
├── pages/product/index.tsx              # the section under the reviews
├── routes/index.tsx                     # /shop/questions, /admin/questions
├── layouts/{seller-layout,admin-layout}/index.tsx     # menu links
├── constants/query-keys/index.ts
└── locales/{vi,en}/{catalog,seller,admin,notifications}.json

bruno/seller/                            # seq 67-77, eleven requests
docs/features/product-questions.md       # new page; plus reference, timeline, backlog, CLAUDE.md
```

**Structure Decision**: The feature sits inside Catalog next to reviews, whose shape it copies - one features file,
one repository with a private `GuardedAsync`, one controller - because it reads the same facts (the product's
seller and listing state) and has the same moderation model.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- **Out of scope by decision** (spec): several answers per question or answers from shoppers, the asker editing or
  deleting their question, votes, and any rate limit beyond the gateway's.
- **The shop's own questions notify nobody.** Staff find them only by opening *To answer* on `/admin/questions`.
- **The public list's gate was the product page** (research D4). Its own address served the questions of an
  unlisted product until specs/081 (#169) closed it the next day.
- **Paging is validated on the public list only.** `GetQuestionsToAnswerQuery` and `GetQuestionsForStaffQuery`
  have no validator for `pageNumber` / `pageSize`; both are behind a role.
- **Seller answers are not reviewed before publication.** A seller's answer is public at once; moderation is after
  the fact, and unlike a product edit (specs/045) an answer does not send anything back to review.
