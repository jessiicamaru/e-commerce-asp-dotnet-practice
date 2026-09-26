# Message Contracts: Product questions

> Written on 2026-09-27, after the feature merged (#160), from the code at that merge, the pull request and docs/features/product-questions.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

The feature **adds no message type and consumes none**. It publishes two existing contracts from
`Ecommerce.Contracts/Activity`, through the shared building blocks every service uses:

- `AuditEntryRecorded`, through `IAuditTrail.RecordAsync` (`Ecommerce.Shared/Audit`, specs/041);
- `UserNotificationRequested`, through `INotifier.NotifyAsync` (`Ecommerce.Shared/Notifications`, specs/042).

What changed is data, not a record: four new notification **kinds**, declared in
`Ecommerce.Shared/Notifications/notification-kinds.json` and as constants on `NotificationKind` in `Notifier.cs`.

---

## How they are published

Both go through Catalog's transactional outbox (`AddEntityFrameworkOutbox<CatalogDbContext>` + `UseBusOutbox()`),
so they commit with the change or not at all (Principle III):

- **Asking** stages the question (`AddAsync`), records the audit entry, sends the notice, then calls
  `SaveChangesAsync` once.
- **Every other change** is a guarded statement; the audit entry and the notice are staged in the `stage` callback,
  which `ProductQuestionRepository.GuardedAsync` runs and saves **only when the statement changed one row**, inside
  the statement's transaction (research D5). A statement that matched nothing publishes nothing - so ten
  simultaneous first answers send one `QuestionAnswered`.

The consumer is the **Activity** service (`RecordAuditEntryConsumer`, `RecordNotificationConsumer`), idempotent on
the publisher's `EntryId` / `NotificationId` (the audit log with `INSERT ... ON CONFLICT DO NOTHING`), so a
redelivery is one entry and one notice. Neither consumer changed for this feature.

---

## `UserNotificationRequested` - the four new kinds

```csharp
record UserNotificationRequested(Guid NotificationId, Guid RecipientId, string Kind,
    Dictionary<string, string> Data, string? Link, DateTime OccurredAt);
```

| Kind | Data (declared `required`) | Recipient | Link | Sent when |
| :--- | :--- | :--- | :--- | :--- |
| `NewQuestion` | `product` | The product's seller | `/shop/questions` | A question is asked on a seller's product. The shop's own product notifies nobody (research D9) |
| `QuestionAnswered` | `product` | The asker | `/products/{productId}` | The **first** answer only, and not when the answerer is the asker (research D6) |
| `QuestionHidden` | `product`, `reason` | The asker | `/products/{productId}` | Staff hide the question |
| `AnswerHidden` | `product`, `reason` | `AnsweredBy` - whoever last wrote the answer | `/products/{productId}` | Staff hide the answer |

`product` is the product's name as stored (the default-language text). The notice carries a kind and data, never a
sentence (specs/042): the storefront words it in whoever reads it now, from `client/src/locales/{vi,en}/notifications.json`,
for example `The seller answered your question about "{{product}}"`. Declaring the kinds in
`notification-kinds.json` is what lets the server tests check every notice published against its declared keys
(specs/048).

Restoring a question or an answer sends no notice.

---

## `AuditEntryRecorded` - seven actions

```csharp
record AuditEntryRecorded(Guid EntryId, string Category, string Action, Guid? ActorId, string? ActorEmail,
    string? ActorRole, string SubjectType, string? SubjectId, string Summary, string? Before, string? After,
    string Service, DateTime OccurredAt);
```

`SubjectType` is `Question`, `SubjectId` the question's id, `Service` is `catalog` (`AddAuditTrail("catalog")`), and
the actor comes from `ICurrentUser`.

| Action | Category | Before | After | Summary |
| :--- | :--- | :--- | :--- | :--- |
| `QuestionAsked` | Catalog | - | `{ Body }` | `A question about "{product}"` |
| `QuestionAnswered` | Catalog | - | `{ Answer }` | `A question about "{product}" answered` |
| `AnswerEdited` | Catalog | `{ Answer }` (the text before) | `{ Answer }` | `The answer about "{product}" rewritten` |
| `QuestionHidden` | Moderation | `{ Hidden: false }` | `{ Hidden: true, Reason }` | `A question hidden: {reason}` |
| `QuestionRestored` | Moderation | `{ Hidden: true, Reason }` | `{ Hidden: false }` | `A question shown again` |
| `AnswerHidden` | Moderation | `{ AnswerHidden: false }` | `{ AnswerHidden: true, Reason }` | `An answer hidden: {reason}` |
| `AnswerRestored` | Moderation | `{ AnswerHidden: true, Reason }` | `{ AnswerHidden: false }` | `An answer shown again` |

The Moderation entries are what a moderator's console reads back as "my decisions" (`GET /api/audit/mine`,
specs/045); the storefront's moderation hooks refresh that list after each move.

---

## Delivery guarantees

| Property | Where it is handled |
| :--- | :--- |
| A notice for a change that did not happen | Impossible: staged only when the guarded statement changed a row, saved in its transaction |
| A change nobody is told about | Impossible: the outbox row commits in the same transaction |
| Same message delivered twice | Activity keys each entry and notice on the publisher's id |
| Two first answers at once | One statement matches; one `QuestionAnswered`; the other becomes a rewrite with an `AnswerEdited` entry |

## Not being added

No `QuestionAskedEvent` or similar integration event: no other service needs to know about questions. A record in
`Ecommerce.Contracts` with no publisher and no consumer would state that a service says something it does not say
(CLAUDE.md).
