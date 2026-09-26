# Message Contracts: Product review before sale

> Written on 2026-09-27, after the feature merged (#97), from the code at that merge, the pull request and docs/features/catalog.md and docs/features/moderation-and-staff.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

**No record in `Ecommerce.Contracts` was added or changed.** The feature publishes two existing
messages from `Ecommerce.Contracts.Activity` in new situations, and adds three notification kinds as
constants in `Ecommerce.Shared/Notifications/Notifier.cs`. Catalog gained `AddNotifier()` in
`Program.cs`: these are the first notices Catalog sends.

---

## Published by Catalog

### `AuditEntryRecorded` - `Ecommerce.Contracts.Activity`

```csharp
record AuditEntryRecorded(Guid EntryId, string Category, string Action, Guid? ActorId, string? ActorEmail,
    string? ActorRole, string SubjectType, string? SubjectId, string Summary, string? Before, string? After,
    string Service, DateTime OccurredAt);
```

Published through `IAuditTrail.RecordAsync` (specs/041), via Catalog's transactional outbox. Consumer:
Activity's `RecordAuditEntryConsumer`, idempotent on `EntryId` (`INSERT ... ON CONFLICT DO NOTHING`).

New actions, all `Category = "Moderation"`, `SubjectType = "Product"`, `SubjectId` = the product id:

| Action | When | Summary | Before → After |
| :-- | :-- | :-- | :-- |
| `ProductApproved` | approve | `"<name>" approved` | `{ReviewStatus: "Pending"}` → `{ReviewStatus: "Approved", Reason: null}` |
| `ProductRejected` | reject | `"<name>" rejected: <reason>` | `{ReviewStatus: "Pending"}` → `{ReviewStatus: "Rejected", Reason}` |
| `ProductTakenDown` | take-down | `"<name>" taken down: <reason>` | `{ReviewStatus: "Approved"}` → `{ReviewStatus: "Rejected", Reason}` |
| `ProductResubmitted` | resubmit | `"<name>" sent back for review` | `{ReviewStatus: "Rejected"}` → `{ReviewStatus: "Pending"}` |
| `ProductSentForReview` | a seller's edit of an approved product | `"<name>" changed after approval and went back to review` | `{ReviewStatus: "Approved"}` → `{ReviewStatus: "Pending"}` |

The actor is whoever made the request (`ICurrentUser`): staff for the first three, the seller (or an
administrator) for resubmit, the seller for the edit. `GET /api/audit/mine` therefore shows a moderator
their decisions and not the sellers' resubmissions.

**Atomicity**: the first four are staged inside `TryReviewAsync`'s transaction, after the guarded
`UPDATE` moved the row and before its one `SaveChangesAsync`; the fifth is staged by the edit handler
before its one save. A move that loses the race stages nothing.

### `UserNotificationRequested` - `Ecommerce.Contracts.Activity`

```csharp
record UserNotificationRequested(Guid NotificationId, Guid RecipientId, string Kind,
    Dictionary<string, string> Data, string? Link, DateTime OccurredAt);
```

Published through `INotifier.NotifyAsync` (specs/042), in the same `stage` as the audit entry. Consumer:
Activity's `RecordNotificationConsumer`, idempotent on `NotificationId`. Sent only when the product has a
seller; the shop's own products have nobody to tell.

| Kind | Recipient | `Data` | `Link` |
| :-- | :-- | :-- | :-- |
| `ProductApproved` | the product's seller | `product` | `/shop/products/{id}` |
| `ProductRejected` | the product's seller | `product`, `reason` | `/shop/products/{id}` |
| `ProductTakenDown` | the product's seller | `product`, `reason` | `/shop/products/{id}` |

The storefront words them (`client/src/locales/{en,vi}/notifications.json`), in English:

- `ProductApproved`: "“{{product}}” is approved and on sale."
- `ProductRejected`: "“{{product}}” was not approved: {{reason}}"
- `ProductTakenDown`: "“{{product}}” was taken off the shelf: {{reason}}"

Resubmitting and the edit hook notify nobody.

> Later: specs/048 declared every kind's data keys in `Ecommerce.Shared/Notifications/notification-kinds.json`;
> these three are there with `product` required, and `reason` too for the two refusals.

---

## Consumed

Nothing new. No service needed a new consumer: Order and Cart learn about review only through the gRPC
`sellable` answer ([grpc.md](./grpc.md)).

## Delivery guarantees

| Property | Where it is handled |
| :--- | :--- |
| Same audit entry or notice delivered twice | Activity inserts on the publisher's id with `ON CONFLICT DO NOTHING` (specs/041, 042) |
| A decision whose transaction fails | Outbox rows roll back with the `UPDATE`; nothing is published |
| Two staff decide at once | Only the request whose `UPDATE` moved a row stages anything |
