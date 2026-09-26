# Message Contract: Notification wording

> Written on 2026-09-27, after the feature merged (#129), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](../spec.md)

No endpoint, gRPC service or message record changed. The interface this feature relies on is the `Data`
of an existing message, and what it adds is a declaration of that data per kind.

## `UserNotificationRequested` - `Ecommerce.Contracts.Activity` (specs/042, unchanged)

```csharp
public record UserNotificationRequested(
    Guid NotificationId,
    Guid RecipientId,
    string Kind,                      // e.g. OrderPaid; the storefront words it
    Dictionary<string, string> Data,  // what the words need, as strings - never a sentence
    string? Link,
    DateTime OccurredAt);
```

**Publishers** (through `INotifier.NotifyAsync`, before the one save): Order (`OrderNotices`), Catalog
(`ProductReviewFeatures`, `ReviewFeatures`), Identity (`ShopApplicationFeatures`, `UserAdministration`).

**Consumer**: Activity's notification consumer (specs/042), which stores `Kind` and `Data` as sent. It does
not read the declaration.

**Reader**: the storefront's `describeNotification`, which turns `Kind` and `Data` into a sentence in the
reader's language.

## What this feature adds to the contract

The keys of `Data` are now declared per `Kind` in
`server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json` (the table is in
[data-model.md](../data-model.md)). The declaration is a **test-time** contract:

| Side | Checked by | Fails when |
| :-- | :-- | :-- |
| Server | `NotificationContract.Problems` in `NotificationTests`, `ProductReviewTests`, `ReviewTests`, `ShopApplicationTests`, `ModerationTests` | a published notice misses a required key or sends an undeclared one |
| Server | `Every_kind_in_code_is_declared_for_the_storefront_and_nothing_else_is` | `NotificationKind` and the declaration name different kinds |
| Storefront | `client/src/utils/notifications/index.test.ts` | a declared kind has no sentence in a language; a sentence leaves `{{`, falls back, or omits a shown value; a declared key has no sample |

Nothing is enforced at run time (research D2): a notice that breaks the declaration is still published and
stored, and the storefront shows the generic sentence rather than a hole.

**How the storefront uses each key**: `orderId` shows as its first 8 characters; `total` and `amount` are
formatted with `currency`, which is never shown alone; `by` reads "you" (`Customer`) or "the shop";
`tracking`, `shop`, `product`, `reason` and `rating` show as sent.
