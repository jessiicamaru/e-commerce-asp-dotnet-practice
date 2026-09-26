# Message Contracts: The notices nobody got

> Written on 2026-09-27, after the feature merged (#142), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../../docs/features/audit-and-notifications.md).

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

No message type was added or changed, and no endpoint. The interface this feature extends is the set of
**notification kinds** - a contract between every publishing service and the storefront, declared in
`server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json` (specs/048).

---

## Published: `UserNotificationRequested` - `Ecommerce.Contracts.Activity`

```csharp
record UserNotificationRequested(
    Guid NotificationId, Guid RecipientId, string Kind,
    Dictionary<string, string> Data, string? Link, DateTime OccurredAt);
```

Published through `INotifier.NotifyAsync` (`Ecommerce.Shared.Notifications`), which stages it in the calling
service's transactional outbox.

| Kind | Publisher | Staged | Data | Link |
| :--- | :--- | :--- | :--- | :--- |
| `AccountLocked` | Identity | before the lock's one save | `until`, `reason` | none |
| `AccountBanned` | Identity | before the ban's one save | `reason` | none |
| `ParcelAutoDelivered` | Order | inside the delivery sweep's transaction (`stage`) | `orderId` | `/shop/sales/{orderId}` |
| `ReviewHidden` | Catalog | inside the guarded hide's transaction (`stage`) | `product`, `reason` | `/products/{productId}` |

The declaration at the merge:

```json
"ParcelAutoDelivered": { "required": ["orderId"] },
"AccountLocked": { "required": ["until", "reason"] },
"AccountBanned": { "required": ["reason"] },
"ReviewHidden": { "required": ["product", "reason"] }
```

Server tests check each published notice with `NotificationContract.Problems(kind, data)`; the storefront's test
holds `describeNotification` to the same file, so a kind or key added on one side only is red on the other.

**Consumer**: Activity's `RecordNotificationConsumer`, unchanged; it stores each notice once
(`ON CONFLICT (Id) DO NOTHING` on `NotificationId`), so a redelivery changes nothing. A person reads their own
through `GET /api/notifications` (specs/042), unchanged.

**Not sent**: `ParcelReceived` for a sweep-delivered parcel (nobody confirmed it), and any notice for the shop's
own parcel.
