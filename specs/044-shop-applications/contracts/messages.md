# Message Contracts: Shop applications

> Written on 2026-09-27, after the feature merged (#96), from the code at that merge, the pull request and
> docs/features/marketplace.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

**No record in `Ecommerce.Contracts` was added or changed.** The feature moves an existing message to a
different moment and uses the two cross-cutting messages every service already publishes. The only new
names are two notification kinds, which are string constants in `Ecommerce.Shared`, not contracts.

All three are published by Identity through its transactional outbox
(`AddEntityFrameworkOutbox<ApplicationDbContext>` with `UseBusOutbox()`, since specs/027), inside the
transaction that holds the decision - so a message can never describe a decision the database did not
accept, and an approval succeeds with RabbitMQ down; the messages leave when it returns.

---

## Published

### `SellerRegisteredEvent` - `Ecommerce.Contracts.Identity` (unchanged shape, new moment)

```csharp
record SellerRegisteredEvent(Guid SellerId, string ShopName, DateTime RegisteredAt);
```

| | Before this feature | After |
| :--- | :--- | :--- |
| Published by | `RegisterSellerCommandHandler`, at registration | `ShopApplicationHandlers` (approve), inside `TryDecideAsync`'s stage |
| `SellerId` | The new account | The applicant's account |
| `ShopName` | From the registration body | The application's `ShopName` |
| `RegisteredAt` | Registration time | The approval's `DecidedAt` |

**Consumer**: Catalog `SellerRegisteredConsumer` (unchanged) → `RecordSellerCommand`, which upserts the
`sellers` read model only when the incoming timestamp is newer (`WHERE "ObservedAt" < @observedAt`), so a
redelivery changes nothing.

**Exactly once per approval**: only the request whose guarded update changed the application row runs the
stage that publishes it; a second approval publishes nothing (`ShopApplicationTests` asserts an empty
list on the second attempt).

`register-seller` **no longer publishes it**; its `IPublishEndpoint` dependency was removed.

### `AuditEntryRecorded` - `Ecommerce.Contracts.Activity` (through `IAuditTrail`)

| Action | Category | Subject | Recorded by | Before / after |
| :--- | :--- | :--- | :--- | :--- |
| `ShopApplied` | User | `ShopApplication`, the application id | Apply handler (actor from the token); `register-seller` (actor: the new account, `AuditActors.Of(user)`) | after: shop name, description, phone (apply); email, names, shop name, roles (register-seller) |
| `ShopApproved` | Moderation | `ShopApplication` | Approve stage | before `{ Status: Pending }`, after `{ Status: Approved, Roles: [...] }` |
| `ShopRejected` | Moderation | `ShopApplication` | Reject stage | before `{ Status: Pending }`, after `{ Status: Rejected, Reason }` |

`register-seller` used to record `ShopOpened` on subject `User`; it records `ShopApplied` now.

**Consumer**: Activity, idempotent on the publisher's `EntryId` (`INSERT ... ON CONFLICT DO NOTHING`,
specs/041).

### `UserNotificationRequested` - `Ecommerce.Contracts.Activity` (through `INotifier`)

| Kind | Recipient | Data | Link |
| :--- | :--- | :--- | :--- |
| `ShopApproved` | The applicant | `shop` | `/shop` |
| `ShopRejected` | The applicant | `shop`, `reason` | `/open-shop` |

Both kinds are new constants in `NotificationKind` (`server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs`).
The storefront words them (`client/src/locales/{en,vi}/notifications.json`): "Your shop “{{shop}}” is
approved. Open it from your menu." / "Your shop “{{shop}}” was not approved: {{reason}}". Later, specs/048
declared each kind's data keys in `notification-kinds.json`; both kinds appear there with exactly these
keys.

**Consumer**: Activity, idempotent on the publisher's `NotificationId` (specs/042).

---

## Consumed

None. Identity consumes nothing new.

---

## Delivery guarantees

| Property | Where it is handled |
| :--- | :--- |
| Two approvals at once | The guarded `UPDATE ... WHERE "Status" = 'Pending'`; only the winner stages any message |
| A message without its decision, or the reverse | Transactional outbox; the stage's writes, the outbox rows and the status change share one transaction |
| Redelivery to Catalog | `sellers` upsert guarded by `ObservedAt` |
| Redelivery to Activity | Unique entry and notification ids |
