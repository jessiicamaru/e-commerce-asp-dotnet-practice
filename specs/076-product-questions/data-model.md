# Phase 1 Data Model: A shopper asks a seller about a product

> Written on 2026-09-27, after the feature merged (#160), from the code at that merge, the pull request and docs/features/product-questions.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One new table in `ecommerce_catalog_db`, added by migration `20260926101826_AddProductQuestions`. No existing
table, column or index changed. The audit entries and notices the feature publishes go through Catalog's existing
transactional outbox tables (`AddTransactionalOutboxEntities()`), which are unchanged.

---

## `product_questions`

One row per question, holding its one answer (research D1, D10). Entity `ProductQuestion` in
`Ecommerce.Catalog.Domain/Entities/ProductQuestion.cs`; mapping in
`Ecommerce.Catalog.Infrastructure/Configurations/ProductQuestionConfiguration.cs`.

### The question

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `Id` | uuid | PK (`PK_product_questions`), `ValueGeneratedNever()` | `Guid.CreateVersion7()` in the entity |
| `ProductId` | uuid | Not null, FK `FK_product_questions_products_ProductId` to `products.Id`, **ON DELETE CASCADE** | The product asked about |
| `AskerId` | uuid | Not null | `ICurrentUser.Id` of the asker - never from the request (research D8) |
| `AskerName` | character varying(100) | Not null | The token's `given_name` when they asked; else the email's initial and a full stop; else `?` |
| `Body` | character varying(1000) | Not null | The question, trimmed. The validator also refuses blank and over 1000 |
| `CreatedAt` | timestamp with time zone | Not null | When it was asked |
| `HiddenAt` | timestamp with time zone | Nullable | Set when staff hide the question; null means visible |
| `HiddenReason` | character varying(500) | Nullable | Why, trimmed; shown to staff and sent to the asker |
| `HiddenBy` | uuid | Nullable | The staff member who hid it |

### The answer

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `Answer` | character varying(2000) | Nullable | The one answer, trimmed; a rewrite replaces it |
| `AnsweredBy` | uuid | Nullable | Who wrote the current text. A rewrite sets it too, so on the shop's own product it is the last staff member to write it |
| `AnsweredAt` | timestamp with time zone | Nullable | The **first** answer: what the asker was told about. A rewrite never moves it |
| `AnswerUpdatedAt` | timestamp with time zone | Nullable | Set with `AnsweredAt` by the first answer, moved by every rewrite |
| `AnswerHiddenAt` | timestamp with time zone | Nullable | Set when staff hide only the answer; locks it (research D3) |
| `AnswerHiddenReason` | character varying(500) | Nullable | Why; sent to whoever answered |
| `AnswerHiddenBy` | uuid | Nullable | The staff member who hid it |

**Derived, not stored**: `answerEdited` in the response is `AnswerUpdatedAt > AnsweredAt + 1 second`. The one
second absorbs the first answer writing both columns from the same `now`.

**Indexes**:

| Name | Columns | Filter | Serves |
| :--- | :--- | :--- | :--- |
| `IX_product_questions_ProductId_CreatedAt` | `(ProductId, CreatedAt)` | - | A product's page, newest first (`GetVisibleAsync`) |
| `IX_product_questions_CreatedAt` | `(CreatedAt)` | `"AnsweredAt" IS NULL AND "HiddenAt" IS NULL` | The unanswered queues, oldest first (`GetQueueAsync`) |

**No CHECK or unique constraint.** One answer per question is structural (the answer lives on the question's row),
and every transition is a guarded `UPDATE` whose `WHERE` carries the state it moves from - the database's row
lock on that one row decides between two writers, not a constraint. Nothing else needs to be unique: a customer may
ask as many questions as they like (spec, Out of scope: rate limiting beyond the gateway's).

---

## State transitions

The question and its answer each have their own states. Every arrow is **one** guarded statement in
`ProductQuestionRepository`, run by `GuardedAsync` inside a transaction inside the execution strategy; its audit
entry and notice are staged and saved only when the statement changed exactly one row (research D5).

```text
Question:    Visible ──hide──▶ Hidden ──restore──▶ Visible

Answer:      None ──first answer──▶ Answered ──rewrite──▶ Answered
                                       │  ▲
                            hide answer│  │restore answer
                                       ▼  │
                                  Answer hidden (locked)
```

| Transition | Statement's guard (`WHERE "Id" = @id AND ...`) | Sets | Staged with it |
| :--- | :--- | :--- | :--- |
| Ask (insert) | - (`AddAsync` + one `SaveChangesAsync`) | the question columns | audit `QuestionAsked`; notice `NewQuestion` to the seller, when there is one |
| First answer | `"AnsweredAt" IS NULL AND "HiddenAt" IS NULL` | `Answer`, `AnsweredBy`, `AnsweredAt`, `AnswerUpdatedAt` | audit `QuestionAnswered`; notice `QuestionAnswered` to the asker (unless the answerer is the asker) |
| Rewrite | `"AnsweredAt" IS NOT NULL AND "AnswerHiddenAt" IS NULL AND "HiddenAt" IS NULL` | `Answer`, `AnsweredBy`, `AnswerUpdatedAt` | audit `AnswerEdited` (before and after); no notice |
| Hide question | `"HiddenAt" IS NULL` | `HiddenAt`, `HiddenReason`, `HiddenBy` | audit `QuestionHidden` (Moderation); notice `QuestionHidden` to the asker |
| Restore question | `"HiddenAt" IS NOT NULL` | the three to null | audit `QuestionRestored` (Moderation) |
| Hide answer | `"AnsweredAt" IS NOT NULL AND "AnswerHiddenAt" IS NULL` | `AnswerHiddenAt`, `AnswerHiddenReason`, `AnswerHiddenBy` | audit `AnswerHidden` (Moderation); notice `AnswerHidden` to `AnsweredBy` |
| Restore answer | `"AnswerHiddenAt" IS NOT NULL` | the three to null | audit `AnswerRestored` (Moderation) |

What a guard matching nothing means:

- **Answer**: the first-answer guard missing falls through to the rewrite (two first answers at once become one
  answer and one rewrite, research D6); the rewrite guard missing too is **409**
  `This question or its answer was hidden by a moderator.` - the question is hidden, or its answer is.
- **Hide / restore**: **409**, each with its own message (see [contracts/http-api.md](./contracts/http-api.md)).

Note that hiding an answer does not require the question to be visible, and hiding a question does not clear its
answer's state: restoring the question brings the answer back as it was.

---

## Reads

| Read | Filter | Order |
| :--- | :--- | :--- |
| `GetVisibleAsync` (the public list) | `ProductId = @id AND "HiddenAt" IS NULL` | `CreatedAt` descending, then `Id` |
| `GetQueueAsync(sellerId, answered)` | joined to `products`: `SellerId = @sellerId` (null for the shop's own) `AND "HiddenAt" IS NULL AND ("AnsweredAt" IS NOT NULL) = @answered` | unanswered: `CreatedAt` ascending (nobody waits longest); answered: `AnsweredAt` descending |
| `GetForStaffAsync(hidden)` | `hidden`: `"HiddenAt" IS NOT NULL OR "AnswerHiddenAt" IS NOT NULL`; else `"HiddenAt" IS NULL` | `CreatedAt` descending |

The public list blanks a hidden answer in code (`QuestionResponse.Public`: `answer` and `answeredAt` null,
`answerEdited` false) rather than in SQL, and never carries reasons or the product's name.

---

## Migration and schema compatibility

`20260926101826_AddProductQuestions` (Catalog Infrastructure) creates the table, its FK and both indexes. `Down`
drops the table.

It is **additive** under the constitution's schema-evolution rule: a new table, nothing dropped, renamed or
narrowed, no `NOT NULL` column added to an existing table. The previously released Catalog image never reads
`product_questions` and runs unchanged against it; rolling Catalog back leaves the table in place, unread.

**What did not change**: `products` gained no column (the answerer rule reads the existing `SellerId`, and the ask
rule the existing `ReviewStatus` / `IsActive`); no integration contract record changed (the feature publishes the
existing `AuditEntryRecorded` and `UserNotificationRequested`, see [contracts/messages.md](./contracts/messages.md)).
